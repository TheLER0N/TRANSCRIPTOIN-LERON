using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

namespace Leron.Audio.Services;

public sealed class WhisperNetService : IWhisperService, IDisposable
{
    private readonly SettingsService _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;
    private string? _factoryModelPath;

    public WhisperNetService(SettingsService settings)
    {
        _settings = settings;
    }

    public async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        var model = ResolveModel()
            ?? throw new FileNotFoundException(
                "Модель ggml-*.bin не найдена. Положи ggml-large-v3-turbo.bin в корень проекта или запусти download-model.bat.");

        await _gate.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();

            if (_factory is null || _factoryModelPath != model)
            {
                _factory?.Dispose();
                _factory = WhisperFactory.FromPath(model);
                _factoryModelPath = model;
            }

            using var processor = _factory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            await using var stream = File.OpenRead(wavPath);
            var sb = new StringBuilder();

            await foreach (var segment in processor.ProcessAsync(stream, ct))
            {
                ct.ThrowIfCancellationRequested();

                var text = segment.Text;
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                sb.Append(text.Trim());
                sb.Append(' ');
            }

            return sb.ToString().Trim();
        }
        finally
        {
            _gate.Release();
        }
    }

    private string? ResolveModel()
    {
        var configured = _settings.Current.ModelPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        return ModelLocator.FindModel();
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _factory = null;
        _gate.Dispose();
    }
}