using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Leron.Audio.Controls;
using Leron.Audio.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Leron.Audio;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var capture = _serviceProvider.GetRequiredService<IAudioCaptureService>();

        var meter = new LevelMeter { Height = 28, Margin = new Thickness(0, 0, 0, 16) };
        meter.Attach(capture);

        var status = new TextBlock
        {
            Text = "Готов. Зажми кнопку для тестовой записи.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 165, 185))
        };

        var button = new Button
        {
            Content = "Держи для записи (тест)",
            Height = 90,
            FontSize = 16,
            Margin = new Thickness(0, 0, 0, 16)
        };

        void StopRecording()
        {
            if (!capture.IsRecording) return;
            var path = capture.Stop();
            status.Text = $"WAV сохранён: {path}";
        }

        button.PreviewMouseLeftButtonDown += (_, _) =>
        {
            capture.Start();
            status.Text = "Запись...";
        };
        button.PreviewMouseLeftButtonUp += (_, _) => StopRecording();
        button.MouseLeave += (_, _) => StopRecording();

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(meter);
        panel.Children.Add(button);
        panel.Children.Add(status);

        var window = new Window
        {
            Title = "LERON-AUDIO",
            Width = 480,
            Height = 640,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.FromRgb(24, 26, 36)),
            Content = panel
        };

        window.Closed += (_, _) => meter.Detach();
        window.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IAudioCaptureService, NAudioCaptureService>();
        // Следующие шаги: IWhisperService, IHotkeyService, IClipboardService,
        // SettingsService, MainViewModel.
    }

    private void OnDispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Необработанная ошибка: {e.Exception.Message}",
            "LERON-AUDIO",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}