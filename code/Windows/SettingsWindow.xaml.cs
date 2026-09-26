// code/Windows/SettingsWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Leron.Audio.ViewModels;

namespace Leron.Audio.Windows;

public partial class SettingsWindow : Window
{
    /// selectedTab — индекс вкладки для шортката из сайдбара главного окна
    /// (0=Общие, 1=Аудио, 2=Клавиши, 3=Хранилище, 4=Словарь).
    public SettingsWindow(SettingsViewModel viewModel, int selectedTab = 0)
    {
        InitializeComponent();
        DataContext = viewModel;
        HotkeyBox.PreviewKeyDown += OnHotkeyCapture;
        Tabs.SelectedIndex = Math.Clamp(selectedTab, 0, 4);
        TryLoadIcon();
    }

    /// Та же логика, что и в MainWindow: подхватывает иконку приложения,
    /// если файл Assets/icon.ico лежит рядом; иначе оставляет дефолтную.
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

    private void OnHotkeyCapture(object sender, KeyEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.CaptureHotkeyCommand.Execute(e);
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => Close();
}