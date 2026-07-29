using System.IO;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;

namespace VoiceInput.Services;

public sealed class AudioRecorder : IDisposable
{
    private const int SampleRate = 16000;
    private const int BitsPerSample = 16;
    private const int ChannelCount = 1;
    private const int BufferMs = 50;
    internal const int BytesPerSecond = SampleRate * ChannelCount * BitsPerSample / 8;

    /// <summary>
    /// 单帧音频时长，等于录音缓冲区长度。VAD 分段用它累计静音时长。
    /// </summary>
    public static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(BufferMs);

    private readonly object _syncRoot = new();
    private WaveInEvent? _waveIn;
    private PushAudioInputStream? _pushStream;
    private MemoryStream? _pcmBuffer;
    private bool _isRecording;

    public event Action<float>? RmsChanged;

    public event Action<string>? ErrorOccurred;

    /// <summary>
    /// 流式模式：音频帧实时推送给 Azure SDK。
    /// </summary>
    public PushAudioInputStream StartStreaming()
    {
        lock (_syncRoot)
        {
            StartLocked(bufferPcm: false);
            return _pushStream!;
        }
    }

    /// <summary>
    /// 缓冲模式：音频帧累积到内存，由 <see cref="DrainWav"/> 分段取走上传。
    /// </summary>
    public void StartBuffering()
    {
        lock (_syncRoot)
        {
            StartLocked(bufferPcm: true);
        }
    }

    /// <summary>
    /// 取走已缓冲的 PCM 并清空缓冲区，返回 WAV 字节流。
    /// 供 VAD 分段在录音进行中切段用；缓冲为空时返回 null。
    /// </summary>
    public byte[]? DrainWav()
    {
        lock (_syncRoot)
        {
            if (_pcmBuffer is null || _pcmBuffer.Length == 0)
            {
                return null;
            }

            var pcmLength = checked((int)_pcmBuffer.Length);
            var wav = WrapPcmInWav(_pcmBuffer.GetBuffer().AsSpan(0, pcmLength));
            _pcmBuffer.SetLength(0);
            return wav;
        }
    }

    /// <summary>
    /// 丢弃当前缓冲的 PCM。仅用于确认缓冲中没有有效语音的场景。
    /// </summary>
    public void ClearBuffer()
    {
        lock (_syncRoot)
        {
            _pcmBuffer?.SetLength(0);
        }
    }

    /// <summary>
    /// 当前缓冲的 PCM 时长。分段器用它判断段落是否够长。
    /// </summary>
    public TimeSpan BufferedDuration
    {
        get
        {
            lock (_syncRoot)
            {
                if (_pcmBuffer is null)
                {
                    return TimeSpan.Zero;
                }

                return TimeSpan.FromSeconds((double)_pcmBuffer.Length / BytesPerSecond);
            }
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            if (!_isRecording)
            {
                return;
            }

            try
            {
                _waveIn?.StopRecording();
            }
            finally
            {
                CleanupDeviceLocked();
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            CleanupDeviceLocked();
            DisposeBufferLocked();
        }
    }

    private void StartLocked(bool bufferPcm)
    {
        if (_isRecording)
        {
            throw new InvalidOperationException("录音已开始。");
        }

        // 每轮录音重置上一轮遗留的缓冲，DrainWav 需要在 Stop 之后仍能取到末段数据，所以不在 Stop 里释放。
        DisposeBufferLocked();

        if (bufferPcm)
        {
            _pcmBuffer = new MemoryStream();
        }
        else
        {
            _pushStream = AudioInputStream.CreatePushStream(
                AudioStreamFormat.GetWaveFormatPCM(SampleRate, BitsPerSample, ChannelCount));
        }

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(SampleRate, BitsPerSample, ChannelCount),
            BufferMilliseconds = BufferMs,
            NumberOfBuffers = 3
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
        _waveIn.StartRecording();

        _isRecording = true;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            _pushStream?.Write(e.Buffer, e.BytesRecorded);

            lock (_syncRoot)
            {
                _pcmBuffer?.Write(e.Buffer, 0, e.BytesRecorded);
            }

            var rms = ComputeRms(e.Buffer, e.BytesRecorded);
            RmsChanged?.Invoke(rms);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"音频写入失败: {ex.Message}");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            ErrorOccurred?.Invoke($"麦克风录制失败: {e.Exception.Message}");
        }
    }

    private void CleanupDeviceLocked()
    {
        _isRecording = false;

        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        if (_pushStream is not null)
        {
            _pushStream.Close();
            _pushStream.Dispose();
            _pushStream = null;
        }
    }

    private void DisposeBufferLocked()
    {
        if (_pcmBuffer is null)
        {
            return;
        }

        _pcmBuffer.Dispose();
        _pcmBuffer = null;
    }

    internal static byte[] WrapPcmInWav(ReadOnlySpan<byte> pcm)
    {
        const int headerSize = 44;
        var byteRate = SampleRate * ChannelCount * BitsPerSample / 8;
        var blockAlign = ChannelCount * BitsPerSample / 8;

        var wav = new byte[checked(headerSize + pcm.Length)];
        using var output = new MemoryStream(wav, writable: true);
        using var writer = new BinaryWriter(output);

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + pcm.Length);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)ChannelCount);
        writer.Write(SampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)BitsPerSample);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(pcm.Length);
        writer.Write(pcm);
        writer.Flush();

        return wav;
    }

    private static float ComputeRms(byte[] buffer, int bytesRecorded)
    {
        if (bytesRecorded <= 0)
        {
            return 0f;
        }

        var sampleCount = bytesRecorded / 2;
        if (sampleCount == 0)
        {
            return 0f;
        }

        var sumSquares = 0d;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(buffer, i * 2);
            var normalized = sample / 32768d;
            sumSquares += normalized * normalized;
        }

        return (float)Math.Sqrt(sumSquares / sampleCount);
    }
}
