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
    private WhisperProcessor? _processor;
    private string? _builtModelPath;

    public WhisperNetService(SettingsService settings)
    {
        _settings = settings;
    }

    public async Task WarmUpAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            EnsureProcessor();
        }
        catch
        {
            // Модель отсутствует или не загрузилась:
            // первая диктовка покажет понятный статус, приложение не падает.
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var processor = EnsureProcessor();

            await using var stream = File.OpenRead(wavPath);
            var sb = new StringBuilder();
            await foreach (var segment in processor.ProcessAsync(stream, ct))
            {
                ct.ThrowIfCancellationRequested();
                var text = segment.Text;
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (sb.Length > 0) sb.Append(' ');
                sb.Append(text.Trim());
            }

            return sb.ToString().Trim();
        }
        finally
        {
            _gate.Release();
        }
    }

    private WhisperProcessor EnsureProcessor()
    {
        var model = ResolveModel()
            ?? throw new FileNotFoundException(
                "Модель ggml-*.bin не найдена. Положи ggml-large-v3-turbo.bin в корень проекта или запусти download-model.bat.");

        // Процессор строится ОДИН раз на модель и переиспользуется всеми диктовками
        if (_processor is not null && _builtModelPath == model)
            return _processor;

        _processor?.Dispose();
        _processor = null;
        _factory?.Dispose();
        _factory = null;

        _factory = WhisperFactory.FromPath(model);

        var language = string.IsNullOrWhiteSpace(_settings.Current.Language)
            ? "auto"
            : _settings.Current.Language;

        // Скорость: потоки = физическим ядрам, температура 0 без fallback-пересэмплирований
        // (TemperatureInc=0 отключает повторные проходы декодера), старый текст не тянется
        // в новые диктовки (NoContext).
        var threads = Math.Max(2, Environment.ProcessorCount / 2);

        _processor = _factory.CreateBuilder()
            .WithLanguage(language)
            .WithNoContext()
            .WithTemperature(0f)
            .WithTemperatureInc(0f)
            .WithThreads(threads)
            .Build();
        _builtModelPath = model;
        return _processor;
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
        _processor?.Dispose();
        _processor = null;
        _factory?.Dispose();
        _factory = null;
        _gate.Dispose();
    }
}