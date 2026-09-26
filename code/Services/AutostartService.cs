// code/Services/AutostartService.cs
using System;
using Microsoft.Win32;

namespace Leron.Audio.Services;

/// Автозапуск с Windows через ключ HKCU\...\Run (без планировщика).
public sealed class AutostartService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LERON-AUDIO";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return key?.GetValue(ValueName) is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (key is null) return;
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exe)) return;
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch
        {
            // Нет прав на реестр: молча пропускаем, приложение работает как обычно
        }
    }
}