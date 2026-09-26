// code/Services/WhisperNetService.cs
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
    private readonly RuntimeService _runtime;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private string? _builtModelPath;
    private string? _builtBackend;
    private string? _builtPromptHash;

    public WhisperNetService(SettingsService settings, RuntimeService runtime)
    {
        _settings = settings;
        _runtime = runtime;
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

    public async Task<TranscriptResult> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var processor = EnsureProcessor();
            await using var stream = File.OpenRead(wavPath);
            var result = new TranscriptResult();
            var sb = new StringBuilder();

            await foreach (var segment in processor.ProcessAsync(stream, ct))
            {
                ct.ThrowIfCancellationRequested();
                var text = segment.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (sb.Length > 0) sb.Append(' ');
                sb.Append(text);

                result.Segments.Add(new TranscriptSegment
                {
                    Start = segment.Start.TotalSeconds,
                    End = segment.End.TotalSeconds,
                    Text = text
                });
            }

            result.Text = sb.ToString().Trim();
            return result;
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

        var backend = _runtime.ResolveBackend(_settings.Current.RuntimeMode);
        
        var prompt = string.IsNullOrWhiteSpace(_settings.Current.InitialPrompt)
            ? string.Empty
            : _settings.Current.InitialPrompt.Trim();
        var promptHash = $"{prompt.Length}:{prompt.GetHashCode():X8}";

        if (_processor is not null 
            && _builtModelPath == model 
            && _builtBackend == backend 
            && _builtPromptHash == promptHash)
            return _processor;

        _processor?.Dispose();
        _processor = null;
        _factory?.Dispose();
        _factory = null;

        _runtime.ApplyBackendPin(backend);
        _factory = WhisperFactory.FromPath(model);

        var language = string.IsNullOrWhiteSpace(_settings.Current.Language)
            ? "auto"
            : _settings.Current.Language;

        var threads = Math.Max(2, Environment.ProcessorCount / 2);

        var builder = _factory.CreateBuilder()
            .WithLanguage(language)
            .WithNoContext()
            .WithTemperature(0f)
            .WithTemperatureInc(0f)
            .WithThreads(threads);

        if (!string.IsNullOrEmpty(prompt))
        {
            builder = builder.WithPrompt(prompt);
        }

        _processor = builder.Build();

        _builtModelPath = model;
        _builtBackend = backend;
        _builtPromptHash = promptHash;
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