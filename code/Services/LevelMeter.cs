// code/Services/LevelMeter.cs
using System;
using System.Windows;
using System.Windows.Media;
using Leron.Audio.Services;

namespace Leron.Audio.Controls;

public sealed class LevelMeter : FrameworkElement
{
    private IAudioCaptureService? _source;
    private float _level;

    /// true — дискретная сегмент-шкала (карточка устройства в сайдбаре),
    /// false — непрерывная полоса (прежнее поведение).
    public bool Segmented { get; set; }
    public int SegmentCount { get; set; } = 14;

    public void Attach(IAudioCaptureService source)
    {
        Detach();
        _source = source;
        _source.SamplesAvailable += OnSamples;
    }

    public void Detach()
    {
        if (_source is null) return;
        _source.SamplesAvailable -= OnSamples;
        _source = null;
    }

    private void OnSamples(float[] samples)
    {
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float a = Math.Abs(samples[i]);
            if (a > peak) peak = a;
        }
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _level = Math.Max(peak, _level * 0.80f);
            InvalidateVisual();
        }));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(new Point(0, 0), RenderSize);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (Segmented)
        {
            RenderSegmented(dc, bounds);
            return;
        }

        // Непрерывная полоса: тёмная подложка + акцентный градиент
        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(16, 22, 23)),   // #101617
            new Pen(new SolidColorBrush(Color.FromRgb(30, 42, 44)), 1), // #1E2A2C
            bounds,
            6, 6);

        double fillWidth = bounds.Width * Math.Clamp(_level, 0f, 1f);
        if (fillWidth > 4)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(15, 118, 110), 0.0));  // #0F766E
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(45, 212, 191), 0.6));  // #2DD4BF
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(124, 245, 227), 1.0)); // #7CF5E3
            var fillRect = new Rect(1, 1, fillWidth - 2, bounds.Height - 2);
            if (fillRect.Width > 0 && fillRect.Height > 0)
            {
                dc.DrawRoundedRectangle(brush, null, fillRect, 4, 4);
            }
        }
    }

    private void RenderSegmented(DrawingContext dc, Rect bounds)
    {
        int count = Math.Max(1, SegmentCount);
        double gap = 3;
        double segWidth = (bounds.Width - (count - 1) * gap) / count;
        if (segWidth <= 0) return;

        int lit = (int)Math.Round(Math.Clamp(_level, 0f, 1f) * count);
        var litBrush = new SolidColorBrush(Color.FromRgb(45, 212, 191));  // #2DD4BF
        var dimBrush = new SolidColorBrush(Color.FromRgb(24, 36, 38));    // #182426

        for (int i = 0; i < count; i++)
        {
            var rect = new Rect(i * (segWidth + gap), 1, segWidth, Math.Max(1, bounds.Height - 2));
            dc.DrawRoundedRectangle(i < lit ? litBrush : dimBrush, null, rect, 3, 3);
        }
    }
}