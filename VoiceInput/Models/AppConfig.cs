namespace VoiceInput.Models;

public sealed class AppConfig
{
    public const string DefaultLanguage = "zh-CN";
    public const string DefaultApiBaseUrl = "https://api.openai.com/v1";
    public const string DefaultModel = "gpt-4o-mini";
    public const string DefaultFallbackHotkey = "RightCtrl";
    public const string ProviderAzure = "Azure";
    public const string ProviderCloudflare = "Cloudflare";
    public const string DefaultSpeechProvider = ProviderAzure;

    public string Language { get; set; } = DefaultLanguage;

    public bool LlmEnabled { get; set; }

    public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = DefaultModel;

    /// <summary>
    /// 语音转写供应商，取值 <see cref="ProviderAzure"/> 或 <see cref="ProviderCloudflare"/>。
    /// </summary>
    public string SpeechProvider { get; set; } = DefaultSpeechProvider;

    public string AzureSpeechKey { get; set; } = string.Empty;

    public string AzureSpeechRegion { get; set; } = string.Empty;

    public string CloudflareAccountId { get; set; } = string.Empty;

    public string CloudflareApiToken { get; set; } = string.Empty;

    public string FallbackHotkey { get; set; } = DefaultFallbackHotkey;

    public bool UsesCloudflare =>
        string.Equals(SpeechProvider, ProviderCloudflare, StringComparison.OrdinalIgnoreCase);

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
            SpeechProvider = SpeechProvider,
            AzureSpeechKey = AzureSpeechKey,
            AzureSpeechRegion = AzureSpeechRegion,
            CloudflareAccountId = CloudflareAccountId,
            CloudflareApiToken = CloudflareApiToken,
            FallbackHotkey = FallbackHotkey
        };
    }
}
