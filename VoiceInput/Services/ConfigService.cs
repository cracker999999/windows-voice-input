using System.IO;
using System.Text;
using System.Text.Json;
using VoiceInput.Models;

namespace VoiceInput.Services;

public sealed class ConfigService
{
    private static readonly string[] SupportedLanguages =
    {
        "en-US",
        "zh-CN",
        "zh-TW",
        "ja-JP",
        "ko-KR"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly object _syncRoot = new();
    private readonly string _configPath;

    public ConfigService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDirectory = Path.Combine(appData, "VoiceInput");
        _configPath = Path.Combine(configDirectory, "config.json");
    }

    public AppConfig Current { get; private set; } = AppConfig.CreateDefault();

    public string ConfigPath => _configPath;

    public string ConfigDirectory => Path.GetDirectoryName(_configPath)!;

    public AppConfig Load()
    {
        lock (_syncRoot)
        {
            if (!File.Exists(_configPath))
            {
                Current = Normalize(AppConfig.CreateDefault());
                SaveNormalizedLocked(Current);
                return Current.Clone();
            }

            try
            {
                var json = File.ReadAllText(_configPath, Encoding.UTF8);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                Current = Normalize(config);
            }
            catch
            {
                Current = Normalize(AppConfig.CreateDefault());
            }

            SaveNormalizedLocked(Current);
            return Current.Clone();
        }
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_syncRoot)
        {
            var normalized = Normalize(config);
            SaveNormalizedLocked(normalized);
            Current = normalized;
        }
    }

    public void Update(Action<AppConfig> updater)
    {
        ArgumentNullException.ThrowIfNull(updater);

        var copy = Current.Clone();
        updater(copy);
        Save(copy);
    }

    private void SaveNormalizedLocked(AppConfig normalized)
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath)!;
            Directory.CreateDirectory(directory);

            var tempPath = _configPath + ".tmp";
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            if (File.Exists(_configPath))
            {
                File.Replace(tempPath, _configPath, null, true);
            }
            else
            {
                File.Move(tempPath, _configPath);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"写入配置文件失败: {_configPath}", ex);
        }
    }

    private static AppConfig Normalize(AppConfig? config)
    {
        config ??= AppConfig.CreateDefault();

        var language = string.IsNullOrWhiteSpace(config.Language)
            ? AppConfig.DefaultLanguage
            : config.Language.Trim();

        if (!SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            language = AppConfig.DefaultLanguage;
        }

        var apiBaseUrl = string.IsNullOrWhiteSpace(config.ApiBaseUrl)
            ? AppConfig.DefaultApiBaseUrl
            : config.ApiBaseUrl.Trim();

        var model = string.IsNullOrWhiteSpace(config.Model)
            ? AppConfig.DefaultModel
            : config.Model.Trim();

        var fallbackHotkey = AppConfig.DefaultFallbackHotkey;

        return new AppConfig
        {
            Language = language,
            LlmEnabled = config.LlmEnabled,
            ApiBaseUrl = apiBaseUrl,
            ApiKey = config.ApiKey ?? string.Empty,
            Model = model,
            AzureSpeechKey = config.AzureSpeechKey?.Trim() ?? string.Empty,
            AzureSpeechRegion = config.AzureSpeechRegion?.Trim() ?? string.Empty,
            FallbackHotkey = fallbackHotkey
        };
    }
}


