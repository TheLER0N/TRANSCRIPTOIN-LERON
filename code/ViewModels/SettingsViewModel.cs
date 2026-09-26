using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Leron.Audio.Services;

namespace Leron.Audio.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IHotkeyService _hotkey;

    [ObservableProperty] private string _hotkeyText = "F4";
    [ObservableProperty] private bool _autoPaste = true;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private string _modelPath = string.Empty;
    [ObservableProperty] private string _modelStatusText = string.Empty;
    [ObservableProperty] private bool _modelStatusIsError;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private IReadOnlyList<string> _microphoneNames = Array.Empty<string>();
    [ObservableProperty] private int _selectedMicrophoneIndex;

    public SettingsViewModel(SettingsService settings, IHotkeyService hotkey)
    {
        _settings = settings;
        _hotkey = hotkey;
        Reload();
    }

    public void Reload()
    {
        HotkeyText = _settings.Current.RecordHotkey;
        AutoPaste = _settings.Current.AutoPaste;
        StartMinimized = _settings.Current.StartMinimized;
        ModelPath = _settings.Current.ModelPath ?? string.Empty;

        var names = new List<string> { "Системный микрофон по умолчанию" };
        names.AddRange(NAudioCaptureService.EnumerateMicrophones());
        MicrophoneNames = names;
        SelectedMicrophoneIndex = Math.Clamp(_settings.Current.MicrophoneId + 1, 0, names.Count - 1);

        ValidateModel();
        StatusMessage = string.Empty;
    }

    partial void OnModelPathChanged(string value) => ValidateModel();

    private void ValidateModel()
    {
        var path = string.IsNullOrWhiteSpace(ModelPath) ? ModelLocator.FindModel() : ModelPath.Trim();
        var found = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        ModelStatusIsError = !found;
        ModelStatusText = found
            ? $"Модель найдена: {path}"
            : "Модель не найдена по указанному пути.";
    }

    [RelayCommand]
    private void CheckModel()
    {
        ValidateModel();
        StatusMessage = ModelStatusText;
    }

    [RelayCommand]
    private void CaptureHotkey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
            case Key.LeftShift:
            case Key.RightShift:
            case Key.LeftAlt:
            case Key.RightAlt:
            case Key.LWin:
            case Key.RWin:
                return;
        }

        var parts = new List<string>();
        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Windows");
        parts.Add(e.Key.ToString());

        HotkeyText = string.Join("+", parts);
        e.Handled = true;
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _hotkey.Register(HotkeyText);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Хоткей не применён: {ex.Message}";
            return;
        }

        _settings.Current.RecordHotkey = HotkeyText;
        _settings.Current.AutoPaste = AutoPaste;
        _settings.Current.StartMinimized = StartMinimized;
        _settings.Current.ModelPath = string.IsNullOrWhiteSpace(ModelPath) ? null : ModelPath.Trim();
        _settings.Current.MicrophoneId = SelectedMicrophoneIndex - 1;
        _settings.Save();
        ValidateModel();
        StatusMessage = "Сохранено. Хоткей и микрофон обновлены без перезапуска.";
    }
}