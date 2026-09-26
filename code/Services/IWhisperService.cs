using System.Threading;
using System.Threading.Tasks;

namespace Leron.Audio.Services;

public interface IWhisperService
{
    Task<string> TranscribeAsync(string wavPath, CancellationToken ct);
}