using System;
using System.IO;
using NAudio.Wave;

namespace Leron.Audio.Services;

public sealed class NAudioCaptureService : IAudioCaptureService, IDisposable
{
    public const int SampleRate = 16000;

    private readonly object _lock = new();
    private WaveInEvent? _waveIn;
    private TempWavWriter? _writer;
    private float _peak;

    public event Action<float[]>? SamplesAvailable;

    public float PeakLevel
    {
        get { lock (_lock) return _peak; }
    }

    public bool IsRecording
    {
        get { lock (_lock) return _waveIn is not null; }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_waveIn is not null) return;

            _peak = 0f;
            var path = Path.Combine(AppContext.BaseDirectory, "temp", "recording.wav");
            _writer = new TempWavWriter(path, SampleRate, 16, 1);

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
        }
    }

    public string Stop()
    {
        lock (_lock)
        {
            if (_waveIn is null) return string.Empty;

            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;

            var path = _writer?.Path ?? string.Empty;
            _writer?.Dispose();
            _writer = null;
            return path;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        float[] samples;
        lock (_lock)
        {
            if (_writer is null) return;
            _writer.WriteSamples(e.Buffer, 0, e.BytesRecorded);

            int count = e.BytesRecorded / 2;
            samples = new float[count];
            float peak = 0f;
            for (int i = 0; i < count; i++)
            {
                float v = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
                samples[i] = v;
                float a = Math.Abs(v);
                if (a > peak) peak = a;
            }
            if (peak > _peak) _peak = peak;
        }

        SamplesAvailable?.Invoke(samples);
    }
}