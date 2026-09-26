using System;

namespace Leron.Audio.Services;

public interface IAudioCaptureService
{
    event Action<float[]>? SamplesAvailable;
    float PeakLevel { get; }
    bool IsRecording { get; }
    void Start();
    string Stop();
}