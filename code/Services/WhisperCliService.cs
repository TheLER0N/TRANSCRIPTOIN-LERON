// code/Services/WhisperCliService.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace Leron.Audio.Services;
public sealed class WhisperCliService : IWhisperService
{
    private const int CrashFailfast = -1073740791; // 0xC0000409: тихий abort внутри whisper-cli
    private const uint GgmlMagic = 0x67676d6cu;    // "ggml"
    private readonly SettingsService _settings;
    public WhisperCliService(SettingsService settings)
    {
        _settings = settings;
    }

    public Task WarmUpAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        var cli = ModelLocator.FindWhisperCli()
            ?? throw new FileNotFoundException(
                "whisper-cli.exe не найден. Запусти download-whisper-cli.bat в корне проекта.");
        var model = ResolveModel()
            ?? throw new FileNotFoundException(
                "Модель ggml-*.bin не найдена. Положи ggml-large-v3-turbo.bin в корень проекта или запусти download-model.bat.");
        ValidateModelFile(model);
        // Только вариации потоков: флагов --no-mmap / --flash-attn в cli v1.8.5 нет
        // (неизвестный флаг = usage + выход без распознавания).
        foreach (var threads in new[] { 4, 1 })
        {
            var result = await RunOnce(cli, model, wavPath, threads, ct);
            if (result.Success) return result.Text;
            if (result.ExitCode != CrashFailfast)
            {
                throw new InvalidOperationException(
                    $"whisper-cli завершился с кодом {result.ExitCode}: {result.ErrorTail} (лог: {result.LogPath})");
            }
        }
        throw new InvalidOperationException(
            "whisper-cli аварийно завершился (0xC0000409) на 4 и 1 потоке. " +
            "Запусти download-whisper-cli.bat force для замены сборки на BLAS; " +
            "если не поможет — удали ggml-large-v3-turbo.bin и запусти download-model.bat.");
    }
    private static void ValidateModelFile(string model)
    {
        var info = new FileInfo(model);
        if (!info.Exists || info.Length < 1_000_000)
        {
            throw new InvalidOperationException(
                $"Модель {model} пустая или слишком маленькая. Удали её и запусти download-model.bat.");
        }
        using var fs = new FileStream(model, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var header = new byte[4];
        var read = fs.Read(header, 0, 4);
        // Магия GGML хранится как uint32 little-endian: байты на диске 6C 6D 67 67.
        uint magic = (uint)(header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24));
        if (read != 4 || magic != GgmlMagic)
        {
            throw new InvalidOperationException(
                $"Модель {model} повреждена (magic {magic:X8} вместо 67676D6C). Удали её и запусти download-model.bat.");
        }
    }
    private sealed record RunResult(
        bool Success, string Text, int ExitCode, string ErrorTail, string LogPath);
    private async Task<RunResult> RunOnce(
        string cli, string model, string wavPath, int threads, CancellationToken ct)
    {
        var outBase = Path.Combine(Path.GetTempPath(), "leron_whisper_" + Guid.NewGuid().ToString("N"));
        var psi = new ProcessStartInfo
        {
            FileName = cli,
            WorkingDirectory = Path.GetDirectoryName(cli) ?? AppContext.BaseDirectory,
            Arguments = $"-m \"{model}\" -f \"{wavPath}\" -otxt -of \"{outBase}\" -l auto -nt -t {threads}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = new Process { StartInfo = psi };
        process.Start();
        var outTask = process.StandardOutput.ReadToEndAsync(ct);
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
        var stdout = await outTask;
        var stderr = await errTask;
        var txtPath = outBase + ".txt";
        if (process.ExitCode == 0 && File.Exists(txtPath))
        {
            var text = await File.ReadAllTextAsync(txtPath, ct);
            try { File.Delete(txtPath); } catch { }
            return new RunResult(true, text.Trim(), 0, string.Empty, string.Empty);
        }
        var logPath = WriteLog(stdout, stderr, process.ExitCode);
        var combined = (stderr + Environment.NewLine + stdout).Trim();
        var tail = combined.Length > 300 ? combined[^300..] : combined;
        return new RunResult(false, string.Empty, process.ExitCode, tail, logPath);
    }
    private string WriteLog(string stdout, string stderr, int exitCode)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "temp");
        Directory.CreateDirectory(dir);
        var logPath = Path.Combine(dir, "whisper_last_log.txt");
        try
        {
            File.WriteAllText(logPath,
                $"ExitCode: {exitCode}{Environment.NewLine}--- STDERR ---{Environment.NewLine}{stderr}{Environment.NewLine}--- STDOUT ---{Environment.NewLine}{stdout}");
        }
        catch
        {
            return string.Empty;
        }
        return logPath;
    }
    private string? ResolveModel()
    {
        var configured = _settings.Current.ModelPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;
        return ModelLocator.FindModel();
    }
}