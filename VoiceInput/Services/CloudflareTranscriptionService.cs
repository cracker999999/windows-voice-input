using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceInput.Models;

namespace VoiceInput.Services;

/// <summary>
/// Cloudflare Workers AI（@cf/openai/whisper-large-v3-turbo）转写服务。
/// 与 Azure 的连续识别不同，这里是一次性上传整段音频，因此没有中间转写结果。
/// </summary>
public sealed class CloudflareTranscriptionService : IDisposable
{
    private const string ModelPath = "@cf/openai/whisper-large-v3-turbo";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whisper 在静音/低信噪音频上会复现训练语料（视频字幕）里的高频尾巴。
    /// 服务端 vad_filter 与阈值调整仍可能漏过，这里做最后一道剔除。
    /// 这些短语在正常口语输入中几乎不会出现，误杀风险可接受。
    /// </summary>
    private static readonly string[] HallucinationPhrases =
    {
        "字幕志愿者",
        "感谢观看",
        "谢谢观看",
        "请不吝点赞",
        "订阅转发",
        "打赏支持明镜与点点栏目",
        "明镜与点点栏目",
        "字幕由Amara.org社区提供",
        "Amara.org",
        "由官方字幕组提供",
        "中文字幕由",
        "本字幕由"
    };

    private readonly ConfigService _configService;
    private readonly HttpClient _httpClient;

    public CloudflareTranscriptionService(ConfigService configService)
    {
        _configService = configService;
        _httpClient = new HttpClient();
    }

    public event Action<string>? ErrorOccurred;

    public bool EnsureConfiguredOrNotify()
    {
        var config = _configService.Current;
        if (!string.IsNullOrWhiteSpace(config.CloudflareAccountId) &&
            !string.IsNullOrWhiteSpace(config.CloudflareApiToken))
        {
            return true;
        }

        ErrorOccurred?.Invoke("Cloudflare Account ID 或 API Token 未配置。");
        return false;
    }

    public async Task<string> TranscribeAsync(
        byte[] wavAudio,
        string language,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wavAudio);

        if (wavAudio.Length == 0)
        {
            return string.Empty;
        }

        var config = _configService.Current.Clone();

        try
        {
            // Base64 与 JSON 构造会随音频长度增长，放到线程池避免阻塞 NAudio 录音回调。
            var responseJson = await Task.Run(
                () => SendTranscriptionRequestAsync(
                    config,
                    wavAudio,
                    language,
                    cancellationToken)).ConfigureAwait(false);

            return ParseTranscription(responseJson);
        }
        catch (OperationCanceledException)
        {
            ErrorOccurred?.Invoke("Cloudflare 转写超时（30秒）。");
            return string.Empty;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Cloudflare 转写失败。", ex);
            ErrorOccurred?.Invoke($"Cloudflare 转写失败: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        AppConfig testConfig,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(testConfig.CloudflareAccountId))
        {
            return (false, "Cloudflare Account ID 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(testConfig.CloudflareApiToken))
        {
            return (false, "Cloudflare API Token 不能为空。");
        }

        try
        {
            // 用 0.5 秒静音探活：只验证鉴权与模型可达，静音返回空文本属正常。
            var silence = BuildSilentWav(TimeSpan.FromMilliseconds(500));
            await SendTranscriptionRequestAsync(
                testConfig,
                silence,
                testConfig.Language,
                cancellationToken).ConfigureAwait(false);

            return (true, "Cloudflare 连接成功。");
        }
        catch (Exception ex)
        {
            return (false, $"Cloudflare 连接失败: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<string> SendTranscriptionRequestAsync(
        AppConfig config,
        byte[] wavAudio,
        string language,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        var endpoint = $"https://api.cloudflare.com/client/v4/accounts/{config.CloudflareAccountId.Trim()}/ai/run/{ModelPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.CloudflareApiToken.Trim());

        var payload = new Dictionary<string, object>
        {
            ["audio"] = Convert.ToBase64String(wavAudio),
            ["task"] = "transcribe",
            // 关闭跨段上下文依赖，减少 Whisper 在短语音上的幻觉重复。
            ["condition_on_previous_text"] = false,
            // 以下三项共同压制 Whisper 的静音幻觉（会吐 "字幕志愿者"、"感谢观看" 等字幕语料尾巴）：
            // vad_filter 让服务端先剔除无语音片段，避免静音被送进解码器。
            ["vad_filter"] = true,
            // 调高无语音判定阈值，让模型更倾向于把边界静音判为无内容而非硬凑文本。
            ["no_speech_threshold"] = 0.8,
            // 收紧压缩比上限，重复堆叠的幻觉文本压缩率高，更容易被过滤掉。
            ["compression_ratio_threshold"] = 2.0
        };

        var whisperLanguage = ToWhisperLanguage(language);
        if (!string.IsNullOrEmpty(whisperLanguage))
        {
            payload["language"] = whisperLanguage;
        }

        var json = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {ExtractApiError(responseBody)}");
        }

        return responseBody;
    }

    /// <summary>
    /// 配置里的语言是 BCP-47（zh-CN），Whisper 需要 ISO 639-1（zh）。
    /// </summary>
    private static string ToWhisperLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        return CultureInfo.GetCultureInfo(language.Trim()).TwoLetterISOLanguageName;
    }

    private static string ParseTranscription(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;

        if (root.TryGetProperty("success", out var success) &&
            success.ValueKind == JsonValueKind.False)
        {
            throw new InvalidOperationException(ExtractApiError(responseJson));
        }

        if (!root.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("text", out var text) ||
            text.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return StripHallucinations(text.GetString()?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// 剔除 Whisper 的静音幻觉文本。
    /// 整段都是幻觉时返回空串；幻觉只在尾部时保留前面的真实内容。
    /// </summary>
    private static string StripHallucinations(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // 幻觉是段尾产物，且内部混有人名等无法穷举的碎片
        // （"...投资人字幕志愿者 杨栋梁感谢观看好谢谢观看"）。
        // 逐短语替换会留下这些碎片，因此从最早命中处直接截断。
        var cutIndex = -1;
        foreach (var phrase in HallucinationPhrases)
        {
            var index = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (cutIndex < 0 || index < cutIndex))
            {
                cutIndex = index;
            }
        }

        if (cutIndex < 0)
        {
            return text;
        }

        return text[..cutIndex].TrimEnd().TrimEnd('，', '。', '、', '！', '？', ',', '.', '!', '?');
    }

    /// <summary>
    /// Cloudflare 错误体形如 { "errors": [ { "code": 1000, "message": "..." } ] }。
    /// </summary>
    private static string ExtractApiError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return "响应为空。";
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array &&
                errors.GetArrayLength() > 0)
            {
                var messages = new List<string>();
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.TryGetProperty("message", out var message) &&
                        message.ValueKind == JsonValueKind.String)
                    {
                        messages.Add(message.GetString() ?? string.Empty);
                    }
                }

                if (messages.Count > 0)
                {
                    return string.Join("; ", messages);
                }
            }
        }
        catch (JsonException)
        {
            // 非 JSON 响应直接回落到原文截断。
        }

        return responseBody.Length > 200 ? responseBody[..200] : responseBody;
    }

    private static byte[] BuildSilentWav(TimeSpan duration)
    {
        var dataSize = checked((int)(AudioRecorder.BytesPerSecond * duration.TotalSeconds));
        return AudioRecorder.WrapPcmInWav(new byte[dataSize]);
    }
}
