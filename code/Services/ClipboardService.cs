// code/Services/ClipboardService.cs
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using WindowsInput;
using WindowsInput.Native;

namespace Leron.Audio.Services;

public sealed class ClipboardService : IClipboardService
{
    private readonly SettingsService _settings;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public ClipboardService(SettingsService settings)
    {
        _settings = settings;
    }

    public Task SetTextAsync(string text)
    {
        Application.Current?.Dispatcher.Invoke(() => Clipboard.SetText(text ?? string.Empty));
        return Task.CompletedTask;
    }

    public async Task PasteIntoActiveWindowAsync()
    {
        if (!_settings.Current.AutoPaste) return;

        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == Environment.ProcessId)
                {
                    // Фокус на самом LERON-AUDIO — пропускаем авто-вставку, текст уже в буфере
                    return;
                }
            }
        }
        catch
        {
            // Fallback: если не удалось определить окно
        }

        await Task.Delay(150);
        try
        {
            var sim = new InputSimulator();
            sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
        }
        catch
        {
            // Fallback: текст уже в буфере, пользователь вставит вручную
        }
    }
}