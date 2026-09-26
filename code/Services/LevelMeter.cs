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

        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(38, 40, 54)),
            null,
            bounds,
            8, 8);

        double fillWidth = bounds.Width * Math.Clamp(_level, 0f, 1f);
        if (fillWidth > 4)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(46, 204, 113), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(241, 196, 15), 0.6));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(231, 76, 60), 1.0));

            dc.DrawRoundedRectangle(
                brush,
                null,
                new Rect(0, 0, fillWidth, bounds.Height),
                8, 8);
        }
    }
}