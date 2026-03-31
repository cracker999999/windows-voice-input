using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using VoiceInput.Models;
using VoiceInput.Services;

namespace VoiceInput.UI;

public partial class SettingsWindow
{
    private readonly ConfigService _configService;
    private readonly LlmRefinementService _llmRefinementService;

    public SettingsWindow(ConfigService configService, LlmRefinementService llmRefinementService)
    {
        _configService = configService;
        _llmRefinementService = llmRefinementService;

        InitializeComponent();
        LoadConfigToUi();
    }

    private void LoadConfigToUi()
    {
        var config = _configService.Current;
        AzureSpeechKeyTextBox.Text = config.AzureSpeechKey;
        AzureSpeechRegionTextBox.Text = config.AzureSpeechRegion;
        ApiBaseUrlTextBox.Text = config.ApiBaseUrl;
        ApiKeyTextBox.Text = config.ApiKey;
        ModelTextBox.Text = config.Model;
    }

    private async void OnTestButtonClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ToggleButtons(false);
            SetStatus("正在测试连接...", false);

            var draftConfig = BuildDraftConfig();
            var result = await _llmRefinementService.TestConnectionAsync(draftConfig);

            SetStatus(result.Message, result.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"测试失败: {ex.Message}", false);
        }
        finally
        {
            ToggleButtons(true);
        }
    }

    private void OnSaveButtonClick(object sender, RoutedEventArgs e)
    {
        var apiBaseUrl = ApiBaseUrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            SetStatus("API Base URL 不能为空。", false);
            return;
        }

        var model = string.IsNullOrWhiteSpace(ModelTextBox.Text)
            ? AppConfig.DefaultModel
            : ModelTextBox.Text.Trim();

        _configService.Update(config =>
        {
            config.AzureSpeechKey = AzureSpeechKeyTextBox.Text?.Trim() ?? string.Empty;
            config.AzureSpeechRegion = AzureSpeechRegionTextBox.Text?.Trim() ?? string.Empty;
            config.ApiBaseUrl = apiBaseUrl;
            config.ApiKey = ApiKeyTextBox.Text ?? string.Empty;
            config.Model = model;
        });

        Close();
    }

    private AppConfig BuildDraftConfig()
    {
        var current = _configService.Current.Clone();
        current.AzureSpeechKey = AzureSpeechKeyTextBox.Text?.Trim() ?? string.Empty;
        current.AzureSpeechRegion = AzureSpeechRegionTextBox.Text?.Trim() ?? string.Empty;
        current.ApiBaseUrl = ApiBaseUrlTextBox.Text.Trim();
        current.ApiKey = ApiKeyTextBox.Text ?? string.Empty;
        current.Model = string.IsNullOrWhiteSpace(ModelTextBox.Text)
            ? AppConfig.DefaultModel
            : ModelTextBox.Text.Trim();

        return current;
    }

    private void ToggleButtons(bool enabled)
    {
        TestButton.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled;
    }

    private void SetStatus(string message, bool success)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = success
            ? Brushes.ForestGreen
            : Brushes.IndianRed;
    }

    private void OnAzureQuotaLinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SetStatus($"打开 Azure Portal 失败: {ex.Message}", false);
        }
        finally
        {
            e.Handled = true;
        }
    }
}
