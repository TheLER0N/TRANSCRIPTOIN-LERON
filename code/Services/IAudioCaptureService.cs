using System;

namespace Leron.Audio.Services;

public interface IAudioCaptureService
{
    event Action<float[]>? SamplesAvailable;
    float PeakLevel { get; }
    bool IsRecording { get; }
    int LastDataBytes { get; }
    void Start();
    string Stop();
}