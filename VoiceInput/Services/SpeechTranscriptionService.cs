using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using VoiceInput.Models;

namespace VoiceInput.Services;

public sealed class SpeechTranscriptionService : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly ConfigService _configService;

    private SpeechRecognizer? _recognizer;
    private TaskCompletionSource<string>? _finalTextSource;
    private string _latestFinalText = string.Empty;
    private string _language;
    private bool _isRecognizing;

    public SpeechTranscriptionService(ConfigService configService)
    {
        _configService = configService;
        _language = configService.Current.Language;
    }

    public event Action<string>? InterimTextUpdated;

    public event Action<string>? FinalTextReady;

    public event Action<string>? ErrorOccurred;

    public string CurrentLanguage => _language;

    public void SetLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return;
        }

        _language = language.Trim();
    }

    public bool EnsureConfiguredOrNotify()
    {
        var config = _configService.Current;
        if (!string.IsNullOrWhiteSpace(config.AzureSpeechKey) &&
            !string.IsNullOrWhiteSpace(config.AzureSpeechRegion))
        {
            return true;
        }

        var message = $"Azure Speech Key 或 Region 未配置，请先编辑: {_configService.ConfigPath}";
        ErrorOccurred?.Invoke(message);
        return false;
    }

    public async Task StartAsync(PushAudioInputStream inputStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);

        if (!EnsureConfiguredOrNotify())
        {
            throw new InvalidOperationException("Azure Speech 配置缺失。");
        }

        SpeechRecognizer recognizer;
        lock (_syncRoot)
        {
            if (_isRecognizing)
            {
                return;
            }

            var config = _configService.Current;
            var speechConfig = SpeechConfig.FromSubscription(config.AzureSpeechKey, config.AzureSpeechRegion);
            speechConfig.SpeechRecognitionLanguage = _language;

            var audioConfig = AudioConfig.FromStreamInput(inputStream);
            recognizer = new SpeechRecognizer(speechConfig, audioConfig);

            recognizer.Recognizing += OnRecognizing;
            recognizer.Recognized += OnRecognized;
            recognizer.Canceled += OnCanceled;
            recognizer.SessionStopped += OnSessionStopped;

            _recognizer = recognizer;
            _finalTextSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _latestFinalText = string.Empty;
            _isRecognizing = true;
        }

        await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
    }

    public async Task<string> StopAsync(CancellationToken cancellationToken = default)
    {
        SpeechRecognizer? recognizer;
        TaskCompletionSource<string>? finalSource;

        lock (_syncRoot)
        {
            recognizer = _recognizer;
            finalSource = _finalTextSource;
            if (!_isRecognizing || recognizer is null)
            {
                return _latestFinalText;
            }
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

            if (finalSource is null)
            {
                return _latestFinalText;
            }

            using var registration = timeoutCts.Token.Register(() => finalSource.TrySetCanceled(timeoutCts.Token));
            var text = await finalSource.Task.ConfigureAwait(false);
            return text;
        }
        catch (OperationCanceledException)
        {
            ErrorOccurred?.Invoke("语音转写超时（10秒）。");
            return _latestFinalText;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"语音转写失败: {ex.Message}");
            return _latestFinalText;
        }
        finally
        {
            CleanupRecognizer();
        }
    }

    public void Dispose()
    {
        CleanupRecognizer();
    }

    private void OnRecognizing(object? sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason != ResultReason.RecognizingSpeech)
        {
            return;
        }

        var text = e.Result.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        InterimTextUpdated?.Invoke(text);
    }

    private void OnRecognized(object? sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason != ResultReason.RecognizedSpeech)
        {
            return;
        }

        var text = e.Result.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _latestFinalText = text;
        FinalTextReady?.Invoke(text);
    }

    private void OnCanceled(object? sender, SpeechRecognitionCanceledEventArgs e)
    {
        if (e.Reason == CancellationReason.Error)
        {
            ErrorOccurred?.Invoke($"Azure Speech 错误: {e.ErrorDetails}");
        }

        _finalTextSource?.TrySetResult(_latestFinalText);
    }

    private void OnSessionStopped(object? sender, SessionEventArgs e)
    {
        _finalTextSource?.TrySetResult(_latestFinalText);
    }

    private void CleanupRecognizer()
    {
        lock (_syncRoot)
        {
            _isRecognizing = false;

            if (_recognizer is not null)
            {
                _recognizer.Recognizing -= OnRecognizing;
                _recognizer.Recognized -= OnRecognized;
                _recognizer.Canceled -= OnCanceled;
                _recognizer.SessionStopped -= OnSessionStopped;
                _recognizer.Dispose();
                _recognizer = null;
            }

            _finalTextSource?.TrySetResult(_latestFinalText);
            _finalTextSource = null;
        }
    }
}
