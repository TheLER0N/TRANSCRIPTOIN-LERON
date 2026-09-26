using System.Threading.Tasks;

namespace Leron.Audio.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task PasteIntoActiveWindowAsync();
}