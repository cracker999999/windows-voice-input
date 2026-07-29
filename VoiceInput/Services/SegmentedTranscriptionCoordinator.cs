using System.Text;

namespace VoiceInput.Services;

/// <summary>
/// Cloudflare 路径的 VAD 分段协调器。
/// whisper 只能整段推理，无法流式，因此在说话停顿处把音频切段并行上传：
/// 录音期间每段说完就发出请求，松键时只需等最后一段，总等待不再与录音时长成正比。
/// </summary>
public sealed class SegmentedTranscriptionCoordinator
{
    /// <summary>RMS 低于此值视为静音。麦克风底噪通常在 0.01 以下。</summary>
    private const float SilenceRmsThreshold = 0.015f;

    /// <summary>连续静音超过此时长判定为句子边界。太短会切碎词语，太长起不到并行效果。</summary>
    private static readonly TimeSpan SilenceDurationForCut = TimeSpan.FromMilliseconds(600);

    /// <summary>段落最短时长。低于此值不切，避免把一个词拆成多段导致 whisper 上下文丢失。</summary>
    private static readonly TimeSpan MinSegmentDuration = TimeSpan.FromSeconds(1.5);

    /// <summary>段落最长时长。超过就强制切，兜住持续说话不停顿的情况。</summary>
    private static readonly TimeSpan MaxSegmentDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 段落内有效语音的最短时长。低于此值视为纯静音/噪声，直接丢弃不上传。
    /// whisper 拿到无语音音频会输出训练语料里的高频字幕文本（"感谢观看" 等幻觉）。
    /// </summary>
    private static readonly TimeSpan MinSpeechInSegment = TimeSpan.FromMilliseconds(300);

    private readonly AudioRecorder _audioRecorder;
    private readonly CloudflareTranscriptionService _cloudflareService;

    private readonly object _syncRoot = new();

    /// <summary>已发出的分段请求，按发出顺序排列，保证拼接顺序与说话顺序一致。</summary>
    private readonly List<Task<string>> _pendingSegments = new();
    private readonly StringBuilder _publishedText = new();

    private TimeSpan _silenceAccumulated;

    /// <summary>当前段落内累计的有效语音时长，切段时用于判断是否值得上传。</summary>
    private TimeSpan _speechAccumulated;

    private string _language = string.Empty;
    private int _publishedSegmentCount;
    private bool _isActive;

    public SegmentedTranscriptionCoordinator(
        AudioRecorder audioRecorder,
        CloudflareTranscriptionService cloudflareService)
    {
        _audioRecorder = audioRecorder;
        _cloudflareService = cloudflareService;
    }

    /// <summary>已完成的分段文本，用于在录音期间给 Overlay 显示进度。</summary>
    public event Action<string>? PartialTextUpdated;

    public void Start(string language)
    {
        lock (_syncRoot)
        {
            _pendingSegments.Clear();
            _publishedText.Clear();
            _silenceAccumulated = TimeSpan.Zero;
            _speechAccumulated = TimeSpan.Zero;
            _language = language;
            _publishedSegmentCount = 0;
            _isActive = true;
        }
    }

    /// <summary>
    /// 接收一帧音频的 RMS。由 AudioRecorder 的 RmsChanged 驱动，
    /// frameDuration 取录音缓冲区长度（BufferMilliseconds）。
    /// </summary>
    public void OnAudioFrame(float rms, TimeSpan frameDuration)
    {
        lock (_syncRoot)
        {
            if (!_isActive)
            {
                return;
            }

            if (rms < SilenceRmsThreshold)
            {
                _silenceAccumulated += frameDuration;
            }
            else
            {
                _silenceAccumulated = TimeSpan.Zero;
                _speechAccumulated += frameDuration;
            }

            var buffered = _audioRecorder.BufferedDuration;
            var hasSpeech = _speechAccumulated >= MinSpeechInSegment;

            // 说完一段后若持续不说话，缓冲里只有静音，攒着既没意义也会触发无谓的强制切段，直接丢弃。
            if (!hasSpeech && buffered >= MaxSegmentDuration)
            {
                _audioRecorder.ClearBuffer();
                _silenceAccumulated = TimeSpan.Zero;
                _speechAccumulated = TimeSpan.Zero;
                return;
            }

            var hitSentenceBoundary = _silenceAccumulated >= SilenceDurationForCut &&
                                      buffered >= MinSegmentDuration;
            var hitMaxLength = buffered >= MaxSegmentDuration;

            // 无语音时不切段：段落只在确实录到人声后才值得发出。
            if (!hasSpeech || (!hitSentenceBoundary && !hitMaxLength))
            {
                return;
            }

            // 切段后重置静音计数，否则持续静音会把后续空缓冲反复切出去。
            _silenceAccumulated = TimeSpan.Zero;
            DispatchSegmentLocked();
        }
    }

    /// <summary>
    /// 松键后调用：切出最后一段，等全部分段返回，按顺序拼接。
    /// </summary>
    public async Task<string> CompleteAsync()
    {
        Task<string>[] segments;

        lock (_syncRoot)
        {
            _isActive = false;
            DispatchSegmentLocked();
            segments = _pendingSegments.ToArray();
        }

        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var texts = await Task.WhenAll(segments).ConfigureAwait(false);
        return JoinSegments(texts, _language);
    }

    /// <summary>
    /// 取走当前缓冲并立即发出转写请求。调用方须持有 _syncRoot。
    /// </summary>
    private void DispatchSegmentLocked()
    {
        // 段内有效语音太少就整段丢弃：Whisper 面对纯静音会吐训练语料里的字幕尾巴
        // （"字幕志愿者"、"感谢观看" 之类），必须在上传前挡掉。
        var hasSpeech = _speechAccumulated >= MinSpeechInSegment;
        _speechAccumulated = TimeSpan.Zero;

        var wav = _audioRecorder.DrainWav();
        if (wav is null || !hasSpeech)
        {
            return;
        }

        var task = _cloudflareService.TranscribeAsync(wav, _language);
        _pendingSegments.Add(task);

        // 单段完成就刷新 Overlay，让用户看到进度而不是干等。
        // 不用 ExecuteSynchronously：请求可能同步失败，那样延续会在持锁线程内重进 _syncRoot。
        _ = task.ContinueWith(_ => RaisePartialText(), TaskScheduler.Default);
    }

    private void RaisePartialText()
    {
        string? text = null;
        lock (_syncRoot)
        {
            var previousLength = _publishedText.Length;
            while (_publishedSegmentCount < _pendingSegments.Count)
            {
                var segment = _pendingSegments[_publishedSegmentCount];
                if (!segment.IsCompletedSuccessfully)
                {
                    break;
                }

                TranscriptionText.AppendTo(_publishedText, segment.Result, _language);
                _publishedSegmentCount++;
            }

            // 只取已完成的前缀，避免乱序显示；前缀没有新增文本时不触发重复 UI 更新。
            if (_publishedText.Length > previousLength)
            {
                text = _publishedText.ToString();
            }
        }

        if (text is null)
        {
            return;
        }

        PartialTextUpdated?.Invoke(text);
    }

    /// <summary>
    /// 拼接分段文本。英文需补空格，中日韩直接相接，规则与 SpeechTranscriptionService 一致。
    /// </summary>
    private static string JoinSegments(string[] texts, string language)
    {
        return TranscriptionText.Join(texts, language);
    }
}
