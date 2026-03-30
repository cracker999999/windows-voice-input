namespace VoiceInput.Models;

public sealed class AppConfig
{
    public const string DefaultLanguage = "zh-CN";
    public const string DefaultApiBaseUrl = "https://api.openai.com/v1";
    public const string DefaultModel = "gpt-4o-mini";
    public const string DefaultFallbackHotkey = "RightCtrl";

    public string Language { get; set; } = DefaultLanguage;

    public bool LlmEnabled { get; set; }

    public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = DefaultModel;

    public string AzureSpeechKey { get; set; } = string.Empty;

    public string AzureSpeechRegion { get; set; } = string.Empty;

    public string FallbackHotkey { get; set; } = DefaultFallbackHotkey;

    public static AppConfig CreateDefault()
    {
        return new AppConfig();
    }

    public AppConfig Clone()
    {
        return new AppConfig
        {
            Language = Language,
            LlmEnabled = LlmEnabled,
            ApiBaseUrl = ApiBaseUrl,
            ApiKey = ApiKey,
            Model = Model,
            AzureSpeechKey = AzureSpeechKey,
            AzureSpeechRegion = AzureSpeechRegion,
            FallbackHotkey = FallbackHotkey
        };
    }
}

