using System;
using System.IO;
using System.Text.Json;

namespace Leron.Audio.Services;

public sealed class Settings
{
    public string RecordHotkey { get; set; } = "F4";
    public bool AutoPaste { get; set; } = true;
    public string? ModelPath { get; set; }
    public string Language { get; set; } = "auto";
    public bool StartMinimized { get; set; } = false;
    public string Theme { get; set; } = "Dark";
    public int MicrophoneId { get; set; } = -1; // -1 = системный микрофон по умолчанию
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