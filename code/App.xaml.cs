using System.Windows;
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

        var window = new Window
        {
            Title = "LERON-AUDIO",
            Width = 480,
            Height = 640,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = "LERON-AUDIO: каркас .NET 8 WPF готов."
        };

        window.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // DI будет заполняться в следующих шагах:
        // IAudioCaptureService, IWhisperService, IHotkeyService,
        // IClipboardService, SettingsService, MainViewModel.
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