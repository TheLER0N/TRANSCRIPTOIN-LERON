// code/MainWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Leron.Audio.Services;
using Leron.Audio.ViewModels;
namespace Leron.Audio;
public partial class MainWindow : Window
{
private readonly IAudioCaptureService _capture;
private readonly MainViewModel _vm;
private readonly ScaleTransform _pttScale = new(1.0, 1.0);
private float _level;
public MainWindow(MainViewModel viewModel, IAudioCaptureService capture)
{
InitializeComponent();
_vm = viewModel;
DataContext = viewModel;
_capture = capture;
LevelMeterSide.Attach(capture);
WaveBars.Attach(capture);
_capture.SamplesAvailable += OnMicSamples;
PttButton.RenderTransform = _pttScale;
TryLoadIcon();
Closed += (_, _) =>
{
_capture.SamplesAvailable -= OnMicSamples;
LevelMeterSide.Detach();
WaveBars.Detach();
};
}
/// Пытается загрузить иконку приложения; если файла нет — оставляет системную.
private void TryLoadIcon()
{
try
{
Icon = new BitmapImage(new Uri("pack://application:,,,/Leron.Audio;component/Assets/icon.ico"));
}
catch
{
// Файл отсутствует или повреждён — остаётся дефолтная иконка окна
}
}
private void OnMicSamples(float[] samples)
{
float peak = 0f;
for (int i = 0; i < samples.Length; i++)
{
float a = Math.Abs(samples[i]);
if (a > peak) peak = a;
}
Dispatcher.BeginInvoke(new Action(() =>
{
_level = Math.Max(peak, _level * 0.75f);
double scale = _vm.IsRecording ? 1.0 + Math.Clamp(_level, 0f, 1f) * 0.06 : 1.0;
_pttScale.ScaleX = scale;
_pttScale.ScaleY = scale;
}));
}
private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
{
if (e.ClickCount == 2)
ToggleMaximize();
else
DragMove();
}
private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
private void OnMaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximize();
private void ToggleMaximize()
{
WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
private void NavRecord_Checked(object sender, RoutedEventArgs e)
{
if (_vm is not null) _vm.CurrentPage = "Record";
}
private void NavHistory_Checked(object sender, RoutedEventArgs e)
{
if (_vm is not null) _vm.CurrentPage = "History";
}
private void NavSettings_Checked(object sender, RoutedEventArgs e)
{
OpenSettings(0);
}
private void OnSettingsClick(object sender, RoutedEventArgs e)
{
NavSettings.IsChecked = true;
OpenSettings(0);
}
/// Настройки открываются страницей внутри этого окна (не диалогом).
private void OpenSettings(int tab)
{
if (_vm is not null) _vm.CurrentPage = "Settings";
SettingsHost.Reload();
SettingsHost.SelectTab(tab);
}
}