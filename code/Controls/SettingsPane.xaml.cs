// code/Controls/SettingsPane.xaml.cs
using System;
using System.Windows.Controls;
using System.Windows.Input;
using Leron.Audio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace Leron.Audio.Controls;
/// Настройки как страница главного окна (не модальный диалог):
/// тот же SettingsViewModel, но хостится внутри MainWindow.
public partial class SettingsPane : UserControl
{
public SettingsPane()
{
InitializeComponent();
DataContext = App.Services.GetRequiredService<SettingsViewModel>();
HotkeyBox.PreviewKeyDown += OnHotkeyCapture;
}
public void SelectTab(int index) => Tabs.SelectedIndex = Math.Clamp(index, 0, 4);
public void Reload() => (DataContext as SettingsViewModel)?.Reload();
private void OnHotkeyCapture(object sender, KeyEventArgs e)
{
if (DataContext is SettingsViewModel vm)
vm.CaptureHotkeyCommand.Execute(e);
}
}