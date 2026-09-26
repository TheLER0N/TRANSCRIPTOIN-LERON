using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Leron.Audio.Services;

public sealed class NHotkeyService : IHotkeyService, IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint LLKHF_UP = 0x80;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // Держим делегат в поле, чтобы GC не убил колбэк хука
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;
    private int _vk;
    private ModifierKeys _mods;
    private bool _held;
    private string? _currentHotkey;

    public event Action? RecordPressed;
    public event Action? RecordReleased;

    public NHotkeyService()
    {
        _proc = HookCallback;
    }

    public void Register(string hotkey)
    {
        Unregister();

        var gesture = ParseHotkey(hotkey);
        _vk = KeyInterop.VirtualKeyFromKey(gesture.Key);
        _mods = gesture.Modifiers;
        _currentHotkey = hotkey;

        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            _currentHotkey = null;
            throw new InvalidOperationException(
                $"Не удалось установить глобальный хук клавиатуры (ошибка {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Unregister()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _held = false;
        _currentHotkey = null;
    }

    public void Dispose()
    {
        Unregister();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _hook != IntPtr.Zero)
        {
            int msg = (int)wParam;
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            if ((int)data.vkCode == _vk)
            {
                bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP || (data.flags & LLKHF_UP) != 0;

                if (!isUp)
                {
                    // Авто-повторы удержания игнорируем: PTT уже начат
                    if (!_held && ModsMatch())
                    {
                        _held = true;
                        RecordPressed?.Invoke();
                    }
                }
                else if (_held)
                {
                    _held = false;
                    RecordReleased?.Invoke();
                }
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool ModsMatch()
    {
        bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        bool win = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;

        return ctrl == _mods.HasFlag(ModifierKeys.Control)
               && shift == _mods.HasFlag(ModifierKeys.Shift)
               && alt == _mods.HasFlag(ModifierKeys.Alt)
               && win == _mods.HasFlag(ModifierKeys.Windows);
    }

    private static KeyGesture ParseHotkey(string hotkey)
    {
        var converter = new KeyGestureConverter();
        var gesture = converter.ConvertFromString(hotkey) as KeyGesture;
        return gesture ?? throw new ArgumentException($"Неверный формат хоткея: {hotkey}");
    }
}