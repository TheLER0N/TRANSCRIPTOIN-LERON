using System;
using System.Windows;
using System.Windows.Media;
using Leron.Audio.Services;

namespace Leron.Audio.Controls;

public sealed class LevelMeter : FrameworkElement
{
    private IAudioCaptureService? _source;
    private float _level;

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

        // Background
        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(22, 22, 26)), // #16161A
            new Pen(new SolidColorBrush(Color.FromRgb(42, 42, 48)), 1), // #2A2A30
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
            // Monochrome gradient: dark gray -> light gray -> white
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(90, 90, 95), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(180, 180, 185), 0.6));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(232, 232, 232), 1.0));

            var fillRect = new Rect(1, 1, fillWidth - 2, bounds.Height - 2);
            if (fillRect.Width > 0 && fillRect.Height > 0)
            {
                dc.DrawRoundedRectangle(
                    brush,
                    null,
                    fillRect,
                    4, 4);
            }
        }
    }
}