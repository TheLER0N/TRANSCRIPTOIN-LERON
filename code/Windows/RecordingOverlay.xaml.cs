// code/Windows/RecordingOverlay.xaml.cs
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Leron.Audio.Services;

namespace Leron.Audio.Windows;

/// Пилюля-оверлей снизу по центру: запись с волной и таймером,
/// «Распознаю…», «микрофон выключен». Не светится в таскбаре,
/// не крадёт фокус и клики (IsHitTestVisible=False).
public partial class RecordingOverlay : Window
{
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _autoHide;
    private readonly Stopwatch _sw = new();

    public RecordingOverlay()
    {
        InitializeComponent();
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => TimerText.Text = _sw.Elapsed.ToString(@"mm\:ss");

        _autoHide = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _autoHide.Tick += (_, _) =>
        {
            _autoHide.Stop();
            HideOverlay();
        };
    }

    public void ShowRecording(IAudioCaptureService capture)
    {
        _autoHide.Stop();
        OverlayBars.Attach(capture);
        SetState(StateRecording);
        _sw.Restart();
        TimerText.Text = "00:00";
        _clock.Start();
        PlaceBottom();
        if (!IsVisible) Show();
        SlideTo(0);
    }

    public void ShowRecognizing()
    {
        _clock.Stop();
        OverlayBars.Detach();
        SetState(StateRecognizing);
        PlaceBottom();
        if (!IsVisible) Show();
        SlideTo(0);
    }

    public void ShowMicOff()
    {
        _clock.Stop();
        OverlayBars.Detach();
        SetState(StateMicOff);
        PlaceBottom();
        if (!IsVisible) Show();
        SlideTo(0);
        _autoHide.Start();
    }

    public void HideOverlay()
    {
        _clock.Stop();
        _autoHide.Stop();
        OverlayBars.Detach();
        SlideTo(160, hideOnComplete: true);
    }

    private void SetState(UIElement active)
    {
        StateRecording.Visibility = ReferenceEquals(active, StateRecording) ? Visibility.Visible : Visibility.Collapsed;
        StateRecognizing.Visibility = ReferenceEquals(active, StateRecognizing) ? Visibility.Visible : Visibility.Collapsed;
        StateMicOff.Visibility = ReferenceEquals(active, StateMicOff) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PlaceBottom()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Bottom - Height - 28;
    }

    private void SlideTo(double targetY, bool hideOnComplete = false)
    {
        var anim = new DoubleAnimation(targetY, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };
        if (hideOnComplete)
            anim.Completed += (_, _) => Hide();

        Root.RenderTransform.BeginAnimation(TranslateTransform.YProperty, anim);
    }
}