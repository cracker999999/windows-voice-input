using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using VoiceInput.Models;
using VoiceInput.Services;

namespace VoiceInput.UI;

public partial class SettingsWindow
{
    private const string RecordingPromptText = "请按下快捷键...";
    private static readonly Brush RecorderIdleBorderBrush = new SolidColorBrush(Color.FromRgb(0xAB, 0xAD, 0xB3));

    private readonly ConfigService _configService;
    private readonly LlmRefinementService _llmRefinementService;
    private readonly CloudflareTranscriptionService _cloudflareService;
    private readonly Action<string> _onHotkeyChanged;
    private readonly Action<bool> _onHotkeyRecordingStateChanged;

    private string _selectedHotkey = AppConfig.DefaultFallbackHotkey;
    private string _hotkeyBeforeRecording = AppConfig.DefaultFallbackHotkey;
    private bool _isHotkeyRecording;

    public SettingsWindow(
        ConfigService configService,
        LlmRefinementService llmRefinementService,
        CloudflareTranscriptionService cloudflareService,
        Action<string> onHotkeyChanged,
        Action<bool> onHotkeyRecordingStateChanged)
    {
        _configService = configService;
        _llmRefinementService = llmRefinementService;
        _cloudflareService = cloudflareService;
        _onHotkeyChanged = onHotkeyChanged;
        _onHotkeyRecordingStateChanged = onHotkeyRecordingStateChanged;

        InitializeComponent();
        LoadConfigToUi();
        Closed += (_, _) => EndHotkeyRecording();
    }

    private void LoadConfigToUi()
    {
        var config = _configService.Current;
        AzureSpeechKeyTextBox.Text = config.AzureSpeechKey;
        AzureSpeechRegionTextBox.Text = config.AzureSpeechRegion;
        CloudflareAccountIdTextBox.Text = config.CloudflareAccountId;
        CloudflareApiTokenTextBox.Text = config.CloudflareApiToken;

        if (config.UsesCloudflare)
        {
            CloudflareProviderRadioButton.IsChecked = true;
        }
        else
        {
            AzureProviderRadioButton.IsChecked = true;
        }

        _selectedHotkey = NormalizeHotkey(config.FallbackHotkey);
        HotkeyRecorderTextBlock.Text = _selectedHotkey;

        ApiBaseUrlTextBox.Text = config.ApiBaseUrl;
        ApiKeyTextBox.Text = config.ApiKey;
        ModelTextBox.Text = config.Model;
    }

    private void OnSpeechProviderChanged(object sender, RoutedEventArgs e)
    {
        // Checked 事件在 InitializeComponent 期间就可能触发，此时面板字段尚未赋值。
        if (AzurePanel is null || CloudflarePanel is null)
        {
            return;
        }

        var useCloudflare = CloudflareProviderRadioButton.IsChecked == true;
        AzurePanel.Visibility = useCloudflare ? Visibility.Collapsed : Visibility.Visible;
        CloudflarePanel.Visibility = useCloudflare ? Visibility.Visible : Visibility.Collapsed;
    }

    private string SelectedProvider =>
        CloudflareProviderRadioButton.IsChecked == true
            ? AppConfig.ProviderCloudflare
            : AppConfig.ProviderAzure;

    private async void OnTestSpeechButtonClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ToggleButtons(false);
            SetStatus("正在测试语音服务...", false);

            var draftConfig = BuildDraftConfig();
            if (draftConfig.UsesCloudflare)
            {
                var result = await _cloudflareService.TestConnectionAsync(draftConfig);
                SetStatus(result.Message, result.Success);
            }
            else
            {
                var result = await SpeechTranscriptionService.TestConnectionAsync(draftConfig);
                SetStatus(result.Message, result.Success);
            }
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
        if (_isHotkeyRecording)
        {
            CancelHotkeyRecording();
        }

        var draftConfig = BuildDraftConfig();
        if (string.IsNullOrWhiteSpace(draftConfig.ApiBaseUrl))
        {
            SetStatus("API Base URL 不能为空。", false);
            return;
        }

        var oldHotkey = _configService.Current.FallbackHotkey;
        var newHotkey = draftConfig.FallbackHotkey;

        _configService.Save(draftConfig);

        if (!string.Equals(oldHotkey, newHotkey, StringComparison.OrdinalIgnoreCase))
        {
            _onHotkeyChanged(newHotkey);
        }

        Close();
    }

    private void OnHotkeyRecorderClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        BeginHotkeyRecording();
    }

    private void BeginHotkeyRecording()
    {
        if (_isHotkeyRecording)
        {
            return;
        }

        _hotkeyBeforeRecording = _selectedHotkey;
        _isHotkeyRecording = true;
        _onHotkeyRecordingStateChanged(true);

        HotkeyRecorderTextBlock.Text = RecordingPromptText;
        HotkeyRecorderBorder.BorderBrush = Brushes.DodgerBlue;

        PreviewKeyDown += OnWindowPreviewKeyDown;
        PreviewKeyUp += OnWindowPreviewKeyUp;
        Keyboard.Focus(HotkeyRecorderBorder);
    }

    private void EndHotkeyRecording()
    {
        if (!_isHotkeyRecording)
        {
            return;
        }

        _isHotkeyRecording = false;
        _onHotkeyRecordingStateChanged(false);
        PreviewKeyDown -= OnWindowPreviewKeyDown;
        PreviewKeyUp -= OnWindowPreviewKeyUp;
        HotkeyRecorderBorder.BorderBrush = RecorderIdleBorderBrush;
    }

    private void CancelHotkeyRecording()
    {
        _selectedHotkey = _hotkeyBeforeRecording;
        HotkeyRecorderTextBlock.Text = _selectedHotkey;
        EndHotkeyRecording();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isHotkeyRecording)
        {
            return;
        }

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            CancelHotkeyRecording();
            return;
        }

        var capturedHotkey = CaptureHotkeyOnKeyDown(key, out var shouldFinalize);
        if (capturedHotkey is null)
        {
            return;
        }

        HotkeyRecorderTextBlock.Text = capturedHotkey;

        if (shouldFinalize)
        {
            _selectedHotkey = NormalizeHotkey(capturedHotkey);
            EndHotkeyRecording();
        }
    }

    private void OnWindowPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_isHotkeyRecording)
        {
            return;
        }

        e.Handled = true;

        if (HasAnyModifierPressed())
        {
            return;
        }

        var displayed = HotkeyRecorderTextBlock.Text;
        if (string.IsNullOrWhiteSpace(displayed) ||
            string.Equals(displayed, RecordingPromptText, StringComparison.Ordinal))
        {
            return;
        }

        _selectedHotkey = NormalizeHotkey(displayed);
        EndHotkeyRecording();
    }

    private static string? CaptureHotkeyOnKeyDown(Key key, out bool shouldFinalize)
    {
        var pressedModifiers = GetPressedModifierKeys();
        RemoveSyntheticCtrlFromAltGr(pressedModifiers);

        if (IsModifierKey(key))
        {
            if (!pressedModifiers.Contains(key))
            {
                pressedModifiers.Add(key);
            }

            RemoveSyntheticCtrlFromAltGr(pressedModifiers);
            shouldFinalize = false;
            return BuildHotkeyString(pressedModifiers, null);
        }

        shouldFinalize = true;
        return BuildHotkeyString(pressedModifiers, key);
    }

    private static void RemoveSyntheticCtrlFromAltGr(List<Key> pressedModifiers)
    {
        if (pressedModifiers.Contains(Key.RightAlt) &&
            pressedModifiers.Contains(Key.RightCtrl) &&
            !Keyboard.IsKeyDown(Key.LeftCtrl))
        {
            pressedModifiers.Remove(Key.RightCtrl);
        }
    }

    private static List<Key> GetPressedModifierKeys()
    {
        var candidates = new[]
        {
            Key.LeftCtrl,
            Key.RightCtrl,
            Key.LeftAlt,
            Key.RightAlt,
            Key.LeftShift,
            Key.RightShift,
            Key.LWin,
            Key.RWin
        };

        var pressed = new List<Key>();
        foreach (var candidate in candidates)
        {
            if (Keyboard.IsKeyDown(candidate))
            {
                pressed.Add(candidate);
            }
        }

        return pressed;
    }

    private static bool HasAnyModifierPressed()
    {
        return Keyboard.IsKeyDown(Key.LeftCtrl) ||
               Keyboard.IsKeyDown(Key.RightCtrl) ||
               Keyboard.IsKeyDown(Key.LeftAlt) ||
               Keyboard.IsKeyDown(Key.RightAlt) ||
               Keyboard.IsKeyDown(Key.LeftShift) ||
               Keyboard.IsKeyDown(Key.RightShift) ||
               Keyboard.IsKeyDown(Key.LWin) ||
               Keyboard.IsKeyDown(Key.RWin);
    }

    private static string BuildHotkeyString(IEnumerable<Key> modifiers, Key? key)
    {
        var parts = new List<string>();

        foreach (var modifier in modifiers)
        {
            var token = KeyToToken(modifier);
            if (!parts.Contains(token, StringComparer.OrdinalIgnoreCase))
            {
                parts.Add(token);
            }
        }

        if (key.HasValue && !IsModifierKey(key.Value))
        {
            parts.Add(KeyToToken(key.Value));
        }

        return parts.Count == 0
            ? AppConfig.DefaultFallbackHotkey
            : string.Join("+", parts);
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or
               Key.LeftAlt or Key.RightAlt or
               Key.LeftShift or Key.RightShift or
               Key.LWin or Key.RWin;
    }

    private static string NormalizeHotkey(string hotkey)
    {
        return string.IsNullOrWhiteSpace(hotkey)
            ? AppConfig.DefaultFallbackHotkey
            : hotkey.Trim();
    }

    private static string KeyToToken(Key key)
    {
        return key switch
        {
            Key.LeftCtrl => "LeftCtrl",
            Key.RightCtrl => "RightCtrl",
            Key.LeftAlt => "LeftAlt",
            Key.RightAlt => "RightAlt",
            Key.LeftShift => "LeftShift",
            Key.RightShift => "RightShift",
            Key.LWin => "LWin",
            Key.RWin => "RWin",
            _ => key.ToString()
        };
    }

    private AppConfig BuildDraftConfig()
    {
        var current = _configService.Current.Clone();
        current.SpeechProvider = SelectedProvider;
        current.AzureSpeechKey = AzureSpeechKeyTextBox.Text?.Trim() ?? string.Empty;
        current.AzureSpeechRegion = AzureSpeechRegionTextBox.Text?.Trim() ?? string.Empty;
        current.CloudflareAccountId = CloudflareAccountIdTextBox.Text?.Trim() ?? string.Empty;
        current.CloudflareApiToken = CloudflareApiTokenTextBox.Text?.Trim() ?? string.Empty;
        current.FallbackHotkey = NormalizeHotkey(_selectedHotkey);
        current.ApiBaseUrl = ApiBaseUrlTextBox.Text.Trim();
        current.ApiKey = ApiKeyTextBox.Text ?? string.Empty;
        current.Model = string.IsNullOrWhiteSpace(ModelTextBox.Text)
            ? AppConfig.DefaultModel
            : ModelTextBox.Text.Trim();

        return current;
    }

    private void ToggleButtons(bool enabled)
    {
        TestSpeechButton.IsEnabled = enabled;
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

    private void OnExternalLinkRequestNavigate(object sender, RequestNavigateEventArgs e)
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
            SetStatus($"打开链接失败: {ex.Message}", false);
        }
        finally
        {
            e.Handled = true;
        }
    }
}
