// code/App.xaml.cs
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Leron.Audio.Services;
using Leron.Audio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Leron.Audio;

public partial class App : Application
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private ServiceProvider? _serviceProvider;
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Тёмный тайтл-бар DWM: применяется ко всем окнам через class-handler
        EventManager.RegisterClassHandler(
            typeof(Window),
            Window.LoadedEvent,
            new RoutedEventHandler(ApplyDarkTitleBar));

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        var settings = _serviceProvider.GetRequiredService<SettingsService>();
        if (settings.Current.StartMinimized)
            window.WindowState = WindowState.Minimized;
        window.Show();

        // Прогрев модели в фоне: первая диктовка не должна висеть
        var whisper = _serviceProvider.GetRequiredService<IWhisperService>();
        _ = Task.Run(async () =>
        {
            try
            {
                await whisper.WarmUpAsync(CancellationToken.None);
            }
            catch
            {
                // Ошибка всплывёт статусом на первой диктовке
            }
        });
    }

    private static void ApplyDarkTitleBar(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window) return;
        try
        {
            var handle = new WindowInteropHelper(window).EnsureHandle();
            int trueValue = 1;
            // Пробуем оба значения: 20 для актуальных Windows 10/11,
            // 19 для ранних сборок Windows 10 1809. Ошибки игнорируем.
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueValue, sizeof(int));
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref trueValue, sizeof(int));
        }
        catch
        {
            // Старые Windows или нет поддержки DWM — тихо игнорируем
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IAudioCaptureService, NAudioCaptureService>();
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