using System;

namespace Leron.Audio.Services;

public interface IHotkeyService
{
    event Action? RecordPressed;
    event Action? RecordReleased;
    void Register(string hotkey);
    void Unregister();
}