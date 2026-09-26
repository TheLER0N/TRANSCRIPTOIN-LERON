// code/Services/IWhisperService.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Leron.Audio.Services;

public sealed class TranscriptSegment
{
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class TranscriptResult
{
    public string Text { get; set; } = string.Empty;
    public List<TranscriptSegment> Segments { get; set; } = new();
}

public interface IWhisperService
{
    Task WarmUpAsync(CancellationToken ct);
    Task<TranscriptResult> TranscribeAsync(string wavPath, CancellationToken ct);
}