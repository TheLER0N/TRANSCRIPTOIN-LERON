using System;
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

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _transcript = string.Empty;

    [ObservableProperty]
    private string _statusText = "Готов. Нажми F4 для записи.";

    public MainViewModel(
        IAudioCaptureService capture,
        IWhisperService whisper,
        IHotkeyService hotkey)
    {
        _capture = capture;
        _whisper = whisper;
        _hotkey = hotkey;

        _hotkey.RecordPressed += OnRecordPressed;
        _hotkey.RecordReleased += OnRecordReleased;

        try
        {
            _hotkey.Register("F4");
            StatusText = "Готов. Нажми F4 для записи.";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка регистрации хоткея: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StartRecording()
    {
        if (IsRecording) return;
        _capture.Start();
        IsRecording = true;
        StatusText = "Запись... Отпусти F4 для распознавания.";
    }

    [RelayCommand]
    private async Task StopRecording()
    {
        if (!IsRecording) return;
        var path = _capture.Stop();
        IsRecording = false;
        StatusText = "Распознаю...";

        try
        {
            var text = await _whisper.TranscribeAsync(path, CancellationToken.None);
            Transcript = string.IsNullOrWhiteSpace(text) ? "Тишина: текст не распознан." : text;
            StatusText = "Готово. Нажми F4 для новой записи.";
        }
        catch (Exception ex)
        {
            Transcript = $"Ошибка: {ex.Message}";
            StatusText = "Ошибка распознавания.";
        }
    }

    private void OnRecordPressed()
    {
        StartRecording();
    }

    private void OnRecordReleased()
    {
        _ = StopRecording();
    }
}