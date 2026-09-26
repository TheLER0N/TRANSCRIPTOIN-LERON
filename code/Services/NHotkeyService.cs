using System;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;

namespace Leron.Audio.Services;

public sealed class NHotkeyService : IHotkeyService, IDisposable
{
    private const string HotkeyName = "LeronRecord";
    private string? _currentHotkey;

    public event Action? RecordPressed;
    public event Action? RecordReleased;

    public void Register(string hotkey)
    {
        Unregister();
        _currentHotkey = hotkey;

        try
        {
            var keyGesture = ParseHotkey(hotkey);
            HotkeyManager.Current.AddOrReplace(HotkeyName, keyGesture.Key, keyGesture.Modifiers, OnHotkeyPressed);
        }
        catch (HotkeyAlreadyRegisteredException)
        {
            throw new InvalidOperationException($"Хоткей {hotkey} уже используется другим приложением.");
        }
    }

    public void Unregister()
    {
        if (_currentHotkey is not null)
        {
            try
            {
                HotkeyManager.Current.Remove(HotkeyName);
            }
            catch
            {
                // Игнорируем ошибки при снятии регистрации
            }
            _currentHotkey = null;
        }
    }

    public void Dispose()
    {
        Unregister();
    }

    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        // NHotkey.Wpf не поддерживает KeyDown/KeyUp для одного хоткея,
        // поэтому эмулируем PTT через таймер или состояние
        RecordPressed?.Invoke();
        e.Handled = true;
    }

    private static KeyGesture ParseHotkey(string hotkey)
    {
        // Простой парсер: "F4", "Ctrl+Shift+F4", "Alt+Space"
        var converter = new KeyGestureConverter();
        var gesture = converter.ConvertFromString(hotkey) as KeyGesture;
        return gesture ?? throw new ArgumentException($"Неверный формат хоткея: {hotkey}");
    }
}