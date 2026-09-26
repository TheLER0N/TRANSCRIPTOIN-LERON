// code/MainWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Leron.Audio.Services;
using Leron.Audio.ViewModels;
using Leron.Audio.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Leron.Audio;

public partial class MainWindow : Window
{
    private readonly IAudioCaptureService _capture;
    private readonly ScaleTransform _pttScale = new(1.0, 1.0);
    private float _level;

    public MainWindow(MainViewModel viewModel, IAudioCaptureService capture)
    {
        InitializeComponent();
        DataContext = viewModel;
        _capture = capture;
        LevelMeter.Attach(capture);
        _capture.SamplesAvailable += OnMicSamples;
        PttButton.RenderTransform = _pttScale;

        // Иконка в тайтл-баре (если файл Assets/icon.ico лежит как Resource)
        TryLoadIcon();

        Closed += (_, _) =>
        {
            _capture.SamplesAvailable -= OnMicSamples;
            LevelMeter.Detach();
        };
    }

    private void TryLoadIcon()
    {
        try
        {
            Icon = new BitmapImage(new Uri("pack://application:,,,/Leron.Audio;component/Assets/icon.ico"));
        }
        catch
        {
            // Файл отсутствует — остаётся дефолтная иконка окна
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
            bool recording = DataContext is MainViewModel vm && vm.IsRecording;
            double scale = recording ? 1.0 + Math.Clamp(_level, 0f, 1f) * 0.06 : 1.0;
            _pttScale.ScaleX = scale;
            _pttScale.ScaleY = scale;
        }));
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var vm = App.Services.GetRequiredService<SettingsViewModel>();
        vm.Reload();
        var win = new SettingsWindow(vm) { Owner = this };
        win.ShowDialog();
    }
}