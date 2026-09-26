// code/Services/IWhisperService.cs
using System.Threading;
using System.Threading.Tasks;

namespace Leron.Audio.Services;

public interface IWhisperService
{
    Task WarmUpAsync(CancellationToken ct);
    Task<string> TranscribeAsync(string wavPath, CancellationToken ct);
}