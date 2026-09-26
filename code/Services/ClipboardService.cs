using System.Threading.Tasks;
using System.Windows;
using WindowsInput;
using WindowsInput.Native;

namespace Leron.Audio.Services;

public sealed class ClipboardService : IClipboardService
{
    private readonly SettingsService _settings;

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