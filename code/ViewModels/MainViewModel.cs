using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Leron.Audio.Services;

namespace Leron.Audio.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAudioCaptureService _capture;
    private readonly IWhisperService _whisper;
    private readonly IHotkeyService _hotkey;
    private readonly IClipboardService _clipboard;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _transcript = string.Empty;

    [ObservableProperty]
    private string _statusText = "Готов.";

    public string RecordButtonText => IsRecording
        ? "● Идёт запись... Отпусти для распознавания"
        : "Нажми и держи F4 (или эту кнопку)";

    public MainViewModel(
        IAudioCaptureService capture,
        IWhisperService whisper,
        IHotkeyService hotkey,
        IClipboardService clipboard,
        SettingsService settings)
    {
        _capture = capture;
        _whisper = whisper;
        _hotkey = hotkey;
        _clipboard = clipboard;
        _settings = settings;

        _hotkey.RecordPressed += OnRecordPressed;
        _hotkey.RecordReleased += OnRecordReleased;

        try
        {
            _hotkey.Register(_settings.Current.RecordHotkey);
            StatusText = $"Готов. Хоткей: {_settings.Current.RecordHotkey}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка регистрации хоткея: {ex.Message}";
        }
    }

    partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(RecordButtonText));
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (IsRecording)
            _ = StopRecording();
        else
            StartRecording();
    }

    [RelayCommand]
    private void StartRecording()
    {
        if (IsRecording) return;
        _capture.Start();
        IsRecording = true;
        StatusText = "Запись...";
    }

    [RelayCommand]
    private async Task StopRecording()
    {
        if (!IsRecording) return;
        var path = _capture.Stop();
        IsRecording = false;

        // Защита от пустой записи: whisper-cli падает на WAV без данных
        if (_capture.LastDataBytes < 9600) // ~0.3 c при 16 кГц 16 бит моно
        {
            Transcript = string.Empty;
            StatusText = "Микрофон не дал звука. Выбери устройство: Настройки → Микрофон.";
            return;
        }

        StatusText = "Распознаю...";
        var sw = Stopwatch.StartNew();
        try
        {
            var text = await _whisper.TranscribeAsync(path, CancellationToken.None);
            sw.Stop();
            var spent = $"Распознано за {sw.Elapsed.TotalSeconds:F1} с.";

            if (string.IsNullOrWhiteSpace(text))
            {
                Transcript = string.Empty;
                StatusText = $"Тишина: текст не распознан. {spent}";
                return;
            }

            Transcript = text;
            await _clipboard.SetTextAsync(text);

            if (_settings.Current.AutoPaste)
            {
                StatusText = $"{spent} Вставляю в активное окно...";
                await _clipboard.PasteIntoActiveWindowAsync();
                StatusText = $"{spent} Текст скопирован и вставлен.";
            }
            else
            {
                StatusText = $"{spent} Текст скопирован в буфер.";
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            Transcript = string.Empty;
            StatusText = $"Ошибка распознавания: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Copy()
    {
        if (string.IsNullOrWhiteSpace(Transcript)) return;
        await _clipboard.SetTextAsync(Transcript);
        StatusText = "Скопировано в буфер.";
    }

    [RelayCommand]
    private void Clear()
    {
        Transcript = string.Empty;
        StatusText = "Очищено.";
    }

    private void OnRecordPressed() => StartRecording();

    private void OnRecordReleased() => _ = StopRecording();
}