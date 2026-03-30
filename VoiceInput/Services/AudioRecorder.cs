using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;

namespace VoiceInput.Services;

public sealed class AudioRecorder : IDisposable
{
    private readonly object _syncRoot = new();
    private WaveInEvent? _waveIn;
    private PushAudioInputStream? _pushStream;
    private bool _isRecording;

    public event Action<float>? RmsChanged;

    public event Action<string>? ErrorOccurred;

    public PushAudioInputStream Start()
    {
        lock (_syncRoot)
        {
            if (_isRecording)
            {
                throw new InvalidOperationException("录音已开始。");
            }

            _pushStream = AudioInputStream.CreatePushStream(
                AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 50,
                NumberOfBuffers = 3
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();

            _isRecording = true;
            return _pushStream;
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
                CleanupLocked();
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            CleanupLocked();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            _pushStream?.Write(e.Buffer, e.BytesRecorded);
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

    private void CleanupLocked()
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
