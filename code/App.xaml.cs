using System;
using System.Windows;
using Leron.Audio.Services;
using Leron.Audio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Leron.Audio;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        var settings = _serviceProvider.GetRequiredService<SettingsService>();
        if (settings.Current.StartMinimized)
            window.WindowState = WindowState.Minimized;
        window.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IAudioCaptureService, NAudioCaptureService>();
        // In-process распознавание через Whisper.Net вместо внешнего whisper-cli.exe
        services.AddSingleton<IWhisperService, WhisperNetService>();
        services.AddSingleton<IHotkeyService, NHotkeyService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindow>();
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
        if (_serviceProvider?.GetService<IHotkeyService>() is IDisposable disposable)
            disposable.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}