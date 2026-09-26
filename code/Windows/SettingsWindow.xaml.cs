using System.Windows;
using System.Windows.Input;
using Leron.Audio.ViewModels;

namespace Leron.Audio.Windows;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        HotkeyBox.PreviewKeyDown += OnHotkeyCapture;
    }

    private void OnHotkeyCapture(object sender, KeyEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.CaptureHotkeyCommand.Execute(e);
    }
}