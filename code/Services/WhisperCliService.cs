using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Leron.Audio.Services;

public sealed class WhisperCliService : IWhisperService
{
    public async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        var cli = ModelLocator.FindWhisperCli()
            ?? throw new FileNotFoundException(
                "whisper-cli.exe не найден. Запусти download-whisper-cli.bat в корне проекта.");

        var model = ModelLocator.FindModel()
            ?? throw new FileNotFoundException(
                "Модель ggml-*.bin не найдена. Положи ggml-large-v3-turbo.bin в корень проекта или запусти download-model.bat.");

        var outBase = Path.Combine(Path.GetTempPath(), "leron_whisper_" + Guid.NewGuid().ToString("N"));

        var psi = new ProcessStartInfo
        {
            FileName = cli,
            Arguments = $"-m \"{model}\" -f \"{wavPath}\" -otxt -of \"{outBase}\" -l auto -np -nt",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var errTask = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var err = await errTask;
        var txtPath = outBase + ".txt";

        if (process.ExitCode != 0 || !File.Exists(txtPath))
        {
            var tail = err.Length > 400 ? err[^400..] : err;
            throw new InvalidOperationException($"whisper-cli завершился с кодом {process.ExitCode}: {tail}");
        }

        var text = await File.ReadAllTextAsync(txtPath, ct);
        try { File.Delete(txtPath); } catch { }

        return text.Trim();
    }
}