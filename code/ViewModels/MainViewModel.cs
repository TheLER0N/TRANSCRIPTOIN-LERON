// code/ViewModels/MainViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Leron.Audio.Services;
using Leron.Audio.Windows;
namespace Leron.Audio.ViewModels;

public sealed class TranscriptSegmentView
{
    public string Timecode { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class HistoryItemVm
{
    public string Id { get; set; } = string.Empty;
    public string WhenText { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double DurationSec { get; set; }
    public int Chars { get; set; }
    public List<HistorySegment> Segments { get; set; } = new();
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAudioCaptureService _capture;
    private readonly IWhisperService _whisper;
    private readonly IHotkeyService _hotkey;
    private readonly IClipboardService _clipboard;
    private readonly SettingsService _settings;
    private readonly HistoryService _history;
    private readonly RuntimeService _runtime;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _recordSw = new();
    private RecordingOverlay? _overlay;
    private string _lastPage = "Record";

    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isRecognizing;
    [ObservableProperty] private string _transcript = string.Empty;
    [ObservableProperty] private string _statusText = "Готов.";
    [ObservableProperty] private string _currentPage = "Record";
    [ObservableProperty] private string _recordTimerText = "00:00:00";
    [ObservableProperty] private string _historySearch = string.Empty;
    [ObservableProperty] private HistoryItemVm? _selectedHistoryItem;

    public ObservableCollection<TranscriptSegmentView> TranscriptSegments { get; } = new();
    public ObservableCollection<HistoryItemVm> HistoryItems { get; } = new();

    public string RecordButtonText => IsRecording
        ? "● Идёт запись... Отпусти для распознавания"
        : "Нажми и держи F4 (или эту кнопку)";
    public string StatusPillText => IsRecording ? "ЗАПИСЬ…" : IsRecognizing ? "РАСПОЗНАЮ…" : "ГОТОВ К ЗАПИСИ";
    public bool ShowRecord => CurrentPage == "Record";
    public bool ShowHistory => CurrentPage == "History";
    public bool ShowSettings => CurrentPage == "Settings";
    public string HotkeyLabel { get; private set; } = "F4";
    public string RecordHint => $"Нажми и держи {HotkeyLabel} (или эту кнопку)";
    public string MicrophoneName { get; private set; } = "Системный микрофон";
    public string LanguageLabel => (_settings.Current.Language ?? "auto").ToUpperInvariant();
    public string NoiseSuppressionText => "Шумоподавление: " + (_settings.Current.NoiseGate ? "ВКЛ" : "ВЫКЛ");
    public int SessionsToday { get; private set; }
    public int WordsToday { get; private set; }
    public string LastSessionText { get; private set; } = "Последняя сессия: 0 символов";

    public MainViewModel(
        IAudioCaptureService capture,
        IWhisperService whisper,
        IHotkeyService hotkey,
        IClipboardService clipboard,
        SettingsService settings,
        HistoryService history,
        RuntimeService runtime)
    {
        _capture = capture;
        _whisper = whisper;
        _hotkey = hotkey;
        _clipboard = clipboard;
        _settings = settings;
        _history = history;
        _runtime = runtime;
        _hotkey.RecordPressed += OnRecordPressed;
        _hotkey.RecordReleased += OnRecordReleased;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RecordTimerText = _recordSw.Elapsed.ToString(@"hh\:mm\:ss");
        try
        {
            _hotkey.Register(_settings.Current.RecordHotkey);
            StatusText = $"Готов. Хоткей: {_settings.Current.RecordHotkey}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка регистрации хоткея: {ex.Message}";
        }
        RefreshCounters();
        RefreshHistory();
    }

    partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(RecordButtonText));
        OnPropertyChanged(nameof(StatusPillText));
    }

    partial void OnIsRecognizingChanged(bool value) => OnPropertyChanged(nameof(StatusPillText));

    partial void OnCurrentPageChanged(string value)
    {
        OnPropertyChanged(nameof(ShowRecord));
        OnPropertyChanged(nameof(ShowHistory));
        OnPropertyChanged(nameof(ShowSettings));
        // Ушли со страницы настроек — подхватываем сохранённое без перезапуска
        if (_lastPage == "Settings" && value != "Settings")
        {
            RefreshCounters();
            RefreshHistory();
        }
        _lastPage = value;
        if (value == "History") RefreshHistory();
    }

    partial void OnHistorySearchChanged(string value) => RefreshHistory();

    partial void OnSelectedHistoryItemChanged(HistoryItemVm? value)
    {
        if (value is null) return;
        Transcript = value.Text;
        TranscriptSegments.Clear();
        foreach (var s in value.Segments)
        {
            TranscriptSegments.Add(new TranscriptSegmentView
            {
                Timecode = TimeSpan.FromSeconds(s.Start).ToString(@"mm\:ss"),
                Text = s.Text
            });
        }
        StatusText = $"Открыта сессия от {value.WhenText} ({value.Chars} символов).";
    }

    public void RefreshCounters()
    {
        HotkeyLabel = _settings.Current.RecordHotkey;
        OnPropertyChanged(nameof(HotkeyLabel));
        OnPropertyChanged(nameof(RecordHint));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(NoiseSuppressionText));
        var names = NAudioCaptureService.EnumerateMicrophones();
        int id = _settings.Current.MicrophoneId;
        MicrophoneName = id >= 0 && id < names.Count ? names[id] : "Системный микрофон";
        OnPropertyChanged(nameof(MicrophoneName));
        SessionsToday = _history.CountToday();
        WordsToday = _history.WordsToday();
        OnPropertyChanged(nameof(SessionsToday));
        OnPropertyChanged(nameof(WordsToday));
        var all = _history.LoadAll();
        LastSessionText = all.Count > 0
            ? $"Последняя сессия: {all[0].Text.Length} символов"
            : "Последняя сессия: 0 символов";
        OnPropertyChanged(nameof(LastSessionText));
    }

    public void RefreshHistory()
    {
        var items = string.IsNullOrWhiteSpace(HistorySearch)
            ? _history.LoadAll()
            : _history.Search(HistorySearch);
        HistoryItems.Clear();
        foreach (var r in items)
        {
            HistoryItems.Add(new HistoryItemVm
            {
                Id = r.Id,
                WhenText = r.Timestamp.ToString("dd.MM HH:mm"),
                Text = r.Text,
                DurationSec = r.DurationSec,
                Chars = r.Text.Length,
                Segments = r.Segments
            });
        }
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
        try
        {
            _capture.Start();
        }
        catch (Exception ex)
        {
            StatusText = $"Микрофон не стартовал: {ex.Message}";
            SafeOverlay(o => o.ShowMicOff());
            return;
        }
        IsRecording = true;
        RecordTimerText = "00:00:00";
        _recordSw.Restart();
        _timer.Start();
        StatusText = "Запись...";
        SafeOverlay(o => o.ShowRecording(_capture));
    }

    [RelayCommand]
    private async Task StopRecording()
    {
        if (!IsRecording) return;
        var path = _capture.Stop();
        IsRecording = false;
        _timer.Stop();
        _recordSw.Stop();
        RecordTimerText = "00:00:00";
        if (_capture.LastDataBytes < 9600)
        {
            Transcript = string.Empty;
            TranscriptSegments.Clear();
            StatusText = "Микрофон не дал звука. Выбери устройство: Настройки → Аудио.";
            SafeOverlay(o => o.ShowMicOff());
            return;
        }
        // Guard тишины: меряем сырой WAV ДО любого DSP (нормализация вытянула бы
        // шум комнаты к -1 dBFS, и Whisper «услышал» бы речь в тишине).
        // Промолчал → результата нет вообще: транскрипт пуст, буфер и вставка
        // не трогаются, сессия не пишется в историю, Whisper не запускается.
        var level = AudioPostProcessor.Measure(path);
        if (level is not null && AudioPostProcessor.IsSilent(level))
        {
            Transcript = string.Empty;
            TranscriptSegments.Clear();
            StatusText = "Тишина: речи не было. Результат не создан, буфер не тронут.";
            SafeOverlay(o => o.HideOverlay());
            return;
        }
        // DSP: постобработка WAV перед распознаванием
        if (_settings.Current.NormalizeAudio || _settings.Current.NoiseGate)
        {
            AudioPostProcessor.TryProcess(path, _settings.Current.NoiseGate, _settings.Current.NormalizeAudio);
        }
        IsRecognizing = true;
        StatusText = "Распознаю...";
        SafeOverlay(o => o.ShowRecognizing());
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _whisper.TranscribeAsync(path, CancellationToken.None);
            var text = result.Text;
            sw.Stop();
            var spent = $"Распознано за {sw.Elapsed.TotalSeconds:F1} с.";
            if (string.IsNullOrWhiteSpace(text))
            {
                Transcript = string.Empty;
                TranscriptSegments.Clear();
                StatusText = $"Тишина: текст не распознан. {spent}";
                SafeOverlay(o => o.HideOverlay());
                return;
            }
            // Слой 2: если тишина прошла по граничной громкости, отбрасываем
            // типовую галлюцинацию Whisper на тишине («в общем, в общем, …»)
            // ДО буфера, вставки и истории.
            if (IsGarbageTranscript(text))
            {
                Transcript = string.Empty;
                TranscriptSegments.Clear();
                StatusText = $"Тишина: ложное распознавание отброшено. {spent}";
                SafeOverlay(o => o.HideOverlay());
                return;
            }
            Transcript = text;
            TranscriptSegments.Clear();
            foreach (var s in result.Segments)
            {
                TranscriptSegments.Add(new TranscriptSegmentView
                {
                    Timecode = TimeSpan.FromSeconds(s.Start).ToString(@"mm\:ss"),
                    Text = s.Text
                });
            }
            _history.Append(new HistoryRecord
            {
                DurationSec = sw.Elapsed.TotalSeconds,
                Text = text,
                Segments = result.Segments.ConvertAll(s => new HistorySegment
                {
                    Start = s.Start,
                    End = s.End,
                    Text = s.Text
                }),
                Model = CurrentModelName(),
                Runtime = _runtime.ResolveBackend(_settings.Current.RuntimeMode)
            });
            RefreshCounters();
            if (CurrentPage == "History") RefreshHistory();
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
            SafeOverlay(o => o.HideOverlay());
        }
        catch (Exception ex)
        {
            sw.Stop();
            Transcript = string.Empty;
            TranscriptSegments.Clear();
            StatusText = $"Ошибка распознавания: {ex.Message}";
            SafeOverlay(o => o.HideOverlay());
        }
        finally
        {
            IsRecognizing = false;
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
        TranscriptSegments.Clear();
        StatusText = "Очищено.";
    }

    private void SafeOverlay(Action<RecordingOverlay> action)
    {
        try
        {
            var app = Application.Current;
            if (app is null) return;
            app.Dispatcher.Invoke(() =>
            {
                _overlay ??= new RecordingOverlay();
                action(_overlay);
            });
        }
        catch
        {
        }
    }

    /// Признак галлюцинации Whisper на тишине: в длинном тексте ничтожно мало
    /// уникальных слов («и, в общем, в общем, в общем…»). Короткие фразы
    /// (< 12 слов) не проверяются — легитимные короткие команды не отбрасываем.
    private static bool IsGarbageTranscript(string text)
    {
        var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 12) return false;
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in words)
        {
            unique.Add(w.TrimEnd('.', ',', '!', '?', ':', ';'));
        }
        return (double)unique.Count / words.Length < 0.25;
    }

    private string CurrentModelName()
    {
        var configured = _settings.Current.ModelPath;
        var path = !string.IsNullOrWhiteSpace(configured) && File.Exists(configured)
            ? configured
            : ModelLocator.FindModel();
        return path is null ? "не найдена" : Path.GetFileName(path);
    }

    private void OnRecordPressed() => StartRecording();
    private void OnRecordReleased() => _ = StopRecording();
}