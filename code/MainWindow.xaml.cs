using System.Windows;
using Leron.Audio.Services;
using Leron.Audio.ViewModels;
using Leron.Audio.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Leron.Audio;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel, IAudioCaptureService capture)
    {
        InitializeComponent();
        DataContext = viewModel;
        LevelMeter.Attach(capture);
        Closed += (_, _) => LevelMeter.Detach();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var vm = App.Services.GetRequiredService<SettingsViewModel>();
        vm.Reload();
        var win = new SettingsWindow(vm) { Owner = this };
        win.ShowDialog();
    }
}