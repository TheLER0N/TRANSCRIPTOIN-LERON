using System.Threading;
using System.Threading.Tasks;

namespace Leron.Audio.Services;

public interface IWhisperService
{
    Task<string> TranscribeAsync(string wavPath, CancellationToken ct);

    // Фоновый прогрев модели/процессора, чтобы первая диктовка не висела.
    // По умолчанию no-op: резервный WhisperCliService прогревать нечего.
    Task WarmUpAsync(CancellationToken ct) => Task.CompletedTask;
}