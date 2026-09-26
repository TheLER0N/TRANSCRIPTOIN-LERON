// code/ViewModels/SettingsViewModel.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Leron.Audio.Services;
namespace Leron.Audio.ViewModels;
public sealed partial class SettingsViewModel : ObservableObject
{
private readonly SettingsService _settings;
private readonly IHotkeyService _hotkey;
private readonly ModelCatalogService _catalog;
private readonly RuntimeService _runtime;
private readonly HistoryService _history;
private readonly AutostartService _autostart;
private CancellationTokenSource? _downloadCts;
// Клавиши и запуск
[ObservableProperty] private string _hotkeyText = "F4";
[ObservableProperty] private bool _autoPaste = true;
[ObservableProperty] private bool _startMinimized;
[ObservableProperty] private bool _autoStartWithWindows;
// Транскрипция: язык и модель
[ObservableProperty] private IReadOnlyList<string> _languageNames = new[] { "Авто (auto)", "Русский (ru)", "Английский (en)" };
[ObservableProperty] private int _selectedLanguageIndex;
[ObservableProperty] private IReadOnlyList<string> _modelNames = Array.Empty<string>();
[ObservableProperty] private int _selectedModelIndex = -1;
[ObservableProperty] private bool _isDownloading;
[ObservableProperty] private double _downloadProgress;
[ObservableProperty] private string _modelStatusText = string.Empty;
[ObservableProperty] private bool _modelStatusIsError;
// Рантайм (GPU/CPU)
[ObservableProperty] private IReadOnlyList<string> _runtimeNames = new[] { "Авто", "CPU", "Vulkan", "CUDA" };
[ObservableProperty] private int _selectedRuntimeIndex;
[ObservableProperty] private string _runtimeDetectedText = string.Empty;
// Аудио
[ObservableProperty] private IReadOnlyList<string> _microphoneNames = Array.Empty<string>();
[ObservableProperty] private int _selectedMicrophoneIndex;
[ObservableProperty] private bool _normalizeAudio = true;
[ObservableProperty] private bool _noiseGate;
// Хранилище
[ObservableProperty] private string _modelsDirText = string.Empty;
[ObservableProperty] private string _historyFileText = string.Empty;
[ObservableProperty] private string _logDirText = string.Empty;
[ObservableProperty] private string _historyFromText = string.Empty;
[ObservableProperty] private string _historyToText = string.Empty;
// Словарь
[ObservableProperty] private string _initialPrompt = string.Empty;
[ObservableProperty] private string _statusMessage = string.Empty;
public SettingsViewModel(
SettingsService settings,
IHotkeyService hotkey,
ModelCatalogService catalog,
RuntimeService runtime,
HistoryService history,
AutostartService autostart)
{
_settings = settings;
_hotkey = hotkey;
_catalog = catalog;
_runtime = runtime;
_history = history;
_autostart = autostart;
Reload();
}
public void Reload()
{
var s = _settings.Current;
HotkeyText = s.RecordHotkey;
AutoPaste = s.AutoPaste;
StartMinimized = s.StartMinimized;
AutoStartWithWindows = _autostart.IsEnabled;
NormalizeAudio = s.NormalizeAudio;
NoiseGate = s.NoiseGate;
SelectedLanguageIndex = s.Language switch { "ru" => 1, "en" => 2, _ => 0 };
SelectedRuntimeIndex = RuntimeService.ParseMode(s.RuntimeMode) switch
{
RuntimeMode.Cpu => 1,
RuntimeMode.Vulkan => 2,
RuntimeMode.Cuda => 3,
_ => 0
};
var names = new List<string> { "Системный микрофон по умолчанию" };
names.AddRange(NAudioCaptureService.EnumerateMicrophones());
MicrophoneNames = names;
SelectedMicrophoneIndex = Math.Clamp(s.MicrophoneId + 1, 0, names.Count - 1);
UpdateRuntimeInfo();
RefreshModelList();
InitialPrompt = s.InitialPrompt ?? string.Empty;
ModelsDirText = _catalog.ModelsDir;
HistoryFileText = _history.FilePath;
LogDirText = Path.Combine(AppContext.BaseDirectory, "temp");
StatusMessage = string.Empty;
}
private void UpdateRuntimeInfo()
{
RuntimeDetectedText =
$"GPU: {_runtime.GpuDescription} | вендор: {_runtime.GpuVendor} | " +
$"доступны рантаймы: {string.Join(", ", _runtime.AvailableRuntimes)} | " +
$"фактический бэкенд: {_runtime.ResolveBackend(_settings.Current.RuntimeMode)}";
}
private void RefreshModelList()
{
var list = new List<string>();
int selected = -1;
var currentPath = _settings.Current.ModelPath;
for (int i = 0; i < ModelCatalogService.Catalog.Count; i++)
{
var m = ModelCatalogService.Catalog[i];
bool downloaded = _catalog.IsDownloaded(m);
list.Add($"{(downloaded ? "✓ " : "   ")}{m.DisplayName} (~{m.ApproxSizeMb} МБ)");
if (currentPath is not null &&
string.Equals(
Path.GetFullPath(currentPath),
Path.GetFullPath(_catalog.GetPath(m)),
StringComparison.OrdinalIgnoreCase))
{
selected = i;
}
}
ModelNames = list;
SelectedModelIndex = selected;
}
partial void OnSelectedModelIndexChanged(int value) => UpdateModelStatus();
private void UpdateModelStatus()
{
if (SelectedModelIndex < 0 || SelectedModelIndex >= ModelCatalogService.Catalog.Count)
{
ModelStatusIsError = false;
ModelStatusText = "Модель из каталога не выбрана: работает ручной путь или автопоиск в корне проекта.";
return;
}
var m = ModelCatalogService.Catalog[SelectedModelIndex];
bool downloaded = _catalog.IsDownloaded(m);
ModelStatusIsError = !downloaded;
ModelStatusText = downloaded
? $"Скачана: {_catalog.GetPath(m)}"
: $"Не скачана. Нажми «Скачать» (~{m.ApproxSizeMb} МБ), затем «Сохранить».";
}
[RelayCommand]
private async Task DownloadSelectedModel()
{
if (IsDownloading) return;
if (SelectedModelIndex < 0 || SelectedModelIndex >= ModelCatalogService.Catalog.Count)
{
StatusMessage = "Сначала выбери модель из списка.";
return;
}
var model = ModelCatalogService.Catalog[SelectedModelIndex];
IsDownloading = true;
DownloadProgress = 0;
_downloadCts = new CancellationTokenSource();
var progress = new Progress<double>(p => DownloadProgress = p);
try
{
await _catalog.DownloadAsync(model, progress, _downloadCts.Token);
StatusMessage = $"Модель {model.Id} скачана и проверена (magic ggml).";
}
catch (OperationCanceledException)
{
StatusMessage = "Загрузка отменена, временный файл удалён.";
}
catch (Exception ex)
{
StatusMessage = $"Ошибка загрузки: {ex.Message}";
}
finally
{
IsDownloading = false;
DownloadProgress = 0;
_downloadCts.Dispose();
_downloadCts = null;
RefreshModelList();
}
}
[RelayCommand]
private void CancelDownload() => _downloadCts?.Cancel();
[RelayCommand]
private void DeleteSelectedModel()
{
if (SelectedModelIndex < 0 || SelectedModelIndex >= ModelCatalogService.Catalog.Count) return;
var model = ModelCatalogService.Catalog[SelectedModelIndex];
_catalog.Delete(model);
RefreshModelList();
StatusMessage = $"Модель {model.Id} удалена из папки models.";
}
[RelayCommand]
private void OpenModelsFolder() => OpenFolder(_catalog.ModelsDir);
[RelayCommand]
private void OpenHistoryFolder() =>
OpenFolder(Path.GetDirectoryName(_history.FilePath) ?? AppContext.BaseDirectory);
private static void OpenFolder(string path)
{
try
{
Directory.CreateDirectory(path);
Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
}
catch
{
// Проводник не открылся — не критично
}
}
[RelayCommand]
private void ClearHistoryAll()
{
_history.ClearAll();
StatusMessage = "История диктовок очищена полностью.";
}
[RelayCommand]
private void ClearHistoryRange()
{
bool okFrom = DateTime.TryParse(HistoryFromText, out var from);
bool okTo = DateTime.TryParse(HistoryToText, out var to);
if (!okFrom || !okTo)
{
StatusMessage = "Диапазон: укажи обе даты в формате дд.мм.гггг.";
return;
}
_history.ClearRange(from, to.AddDays(1).AddSeconds(-1));
StatusMessage = $"История с {from:dd.MM.yyyy} по {to:dd.MM.yyyy} удалена.";
}
[RelayCommand]
private void RunDiagnostics()
{
var sb = new StringBuilder();
sb.Append("Модель: ");
var model = ResolveCurrentModel();
sb.Append(model is not null ? Path.GetFileName(model) : "НЕ НАЙДЕНА");
sb.Append(" | Бэкенд: ").Append(_runtime.ResolveBackend(_settings.Current.RuntimeMode));
sb.Append(" | GPU: ").Append(_runtime.GpuVendor);
sb.Append(" | Микрофонов: ").Append(Math.Max(0, MicrophoneNames.Count - 1));
sb.Append(" | Выбран: ").Append(SelectedMicrophoneIndex == 0
? "системный"
: MicrophoneNames[SelectedMicrophoneIndex]);
sb.Append(" | Потоков: ").Append(Math.Max(2, Environment.ProcessorCount / 2));
StatusMessage = sb.ToString();
}
private string? ResolveCurrentModel()
{
var configured = _settings.Current.ModelPath;
if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
return ModelLocator.FindModel();
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
var s = _settings.Current;
s.RecordHotkey = HotkeyText;
s.AutoPaste = AutoPaste;
s.StartMinimized = StartMinimized;
s.NormalizeAudio = NormalizeAudio;
s.NoiseGate = NoiseGate;
s.Language = SelectedLanguageIndex switch { 1 => "ru", 2 => "en", _ => "auto" };
s.MicrophoneId = SelectedMicrophoneIndex - 1;
s.RuntimeMode = SelectedRuntimeIndex switch { 1 => "cpu", 2 => "vulkan", 3 => "cuda", _ => "auto" };
if (SelectedModelIndex >= 0 && SelectedModelIndex < ModelCatalogService.Catalog.Count)
{
var model = ModelCatalogService.Catalog[SelectedModelIndex];
if (_catalog.IsDownloaded(model)) s.ModelPath = _catalog.GetPath(model);
}
s.InitialPrompt = string.IsNullOrWhiteSpace(InitialPrompt)
? Settings.DefaultInitialPrompt
: InitialPrompt.Trim();
_settings.Save();
_autostart.SetEnabled(AutoStartWithWindows);
UpdateRuntimeInfo();
UpdateModelStatus();
StatusMessage = "Сохранено. Модель, рантайм, микрофон, автозапуск и словарь применены без перезапуска.";
}
}