using System;
using System.IO;
using System.Text.Json;

namespace Leron.Audio.Services;

public sealed class Settings
{
    public const string DefaultInitialPrompt =
        "Русская речь с английскими техническими терминами без транслита: " +
        "commit, deploy, backend, frontend, breakpoint, debug, runtime, build, release, " +
        "framework, library, package, dependency, NuGet, API, JSON, token, hotkey, clipboard, " +
        "microphone, whisper, model, GPU, CPU, RAM, SSD, IDE, Visual Studio, GitHub, repository, " +
        "branch, merge, rebase, pull request, code review, unit test, performance, latency, " +
        "thread, process, cache, database, query, endpoint, auth, login, password, settings, " +
        "UI, GUI, WPF, .NET, Windows, Docker, container, server, client, HTTP, REST, SQL, " +
        "script, batch, file, folder, path, log, console, terminal, error, warning, exception, " +
        "stack trace, refactoring, interface, class, method, function, variable, string, " +
        "async, await, callback, event, handler, service, singleton, factory, observer.";

    public string RecordHotkey { get; set; } = "F4";
    public bool AutoPaste { get; set; } = true;
    public string? ModelPath { get; set; }
    public string Language { get; set; } = "auto";
    public bool StartMinimized { get; set; } = false;
    public string Theme { get; set; } = "Dark";
    public int MicrophoneId { get; set; } = -1; // -1 = системный микрофон по умолчанию
    public string InitialPrompt { get; set; } = DefaultInitialPrompt;
}

public sealed class SettingsService
{
    private readonly string _path;

    public Settings Current { get; private set; } = new();

    public SettingsService()
    {
        _path = Path.Combine(AppContext.BaseDirectory, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                Current = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch
        {
            Current = new Settings();
        }
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Current, options);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Игнорируем ошибки записи (например, нет прав)
        }
    }
}