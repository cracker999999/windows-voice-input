using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceInput.Models;

namespace VoiceInput.Services;

public sealed class LlmRefinementService : IDisposable
{
    private const string SystemPrompt = "你是语音识别后处理助手。只修复明显的语音识别错误：中文谐音导致的技术术语错误与明显同音字错误。绝对禁止改写句式、润色措辞、删改内容、添加标点。若原文正确则原样返回。仅返回最终文本。";

    private readonly ConfigService _configService;
    private readonly HttpClient _httpClient;

    private bool _enabled;

    public LlmRefinementService(ConfigService configService)
    {
        _configService = configService;
        _httpClient = new HttpClient();
        _enabled = configService.Current.LlmEnabled;
    }

    public bool IsEnabled => _enabled;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }

    public async Task<string> RefineAsync(string originalText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originalText) || !_enabled)
        {
            return originalText;
        }

        var config = _configService.Current;
        if (string.IsNullOrWhiteSpace(config.ApiBaseUrl) ||
            string.IsNullOrWhiteSpace(config.ApiKey) ||
            string.IsNullOrWhiteSpace(config.Model))
        {
            return originalText;
        }

        try
        {
            var responseJson = await SendChatRequestAsync(config, originalText, cancellationToken).ConfigureAwait(false);
            var refined = ParseChatCompletion(responseJson);
            return string.IsNullOrWhiteSpace(refined) ? originalText : refined;
        }
        catch (OperationCanceledException)
        {
            return originalText;
        }
        catch
        {
            return originalText;
        }
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(AppConfig testConfig, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(testConfig.ApiBaseUrl))
        {
            return (false, "API Base URL 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(testConfig.ApiKey))
        {
            return (false, "API Key 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(testConfig.Model))
        {
            return (false, "Model 不能为空。");
        }

        try
        {
            var responseJson = await SendChatRequestAsync(testConfig, "test", cancellationToken).ConfigureAwait(false);
            var content = ParseChatCompletion(responseJson);
            return string.IsNullOrWhiteSpace(content)
                ? (false, "API 返回空内容。")
                : (true, "连接成功。");
        }
        catch (Exception ex)
        {
            return (false, $"连接失败: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<string> SendChatRequestAsync(AppConfig config, string inputText, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        var endpoint = BuildEndpoint(config.ApiBaseUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var payload = new
        {
            model = config.Model,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = inputText }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return responseBody;
    }

    private static string BuildEndpoint(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"{trimmed}/chat/completions";
    }

    private static string ParseChatCompletion(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);

        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var choice = choices[0];
        if (!choice.TryGetProperty("message", out var message))
        {
            return string.Empty;
        }

        if (!message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString()?.Trim() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("text", out var textProperty) &&
                textProperty.ValueKind == JsonValueKind.String)
            {
                builder.Append(textProperty.GetString());
            }
        }

        return builder.ToString().Trim();
    }
}

