// code/Services/LevelMeter.cs
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Leron.Audio.Services;

namespace Leron.Audio.Controls;

public sealed class LevelMeter : FrameworkElement
{
    private const int BarCount = 48;
    /// Частота продвижения кольца: совпадает с кадаксом буферов захвата (BufferMilliseconds=50).
    private const int TickMs = 50;
    /// Порог «бар горит» — тот же, что в отрисовке RenderBars.
    private const float BarEpsilon = 0.004f;

    private IAudioCaptureService? _source;
    private float _level;
    private readonly float[] _bars = new float[BarCount];
    private int _barIndex;

    // Таймер скролла волны: единственная точка продвижения кольцевого буфера.
    // Во время записи подхватывает пики из SamplesAvailable; после окончания
    // записи продолжает писать нули — след «уплывает» с экрана; когда кольцо
    // полностью пусто и новых сэмплов нет — останавливает сам себя
    // (анимация прерывается, простоя CPU нет).
    private readonly DispatcherTimer _scrollTimer;
    private float _pendingPeak;

    /// true — дискретная сегмент-шкала (карточка устройства в сайдбаре),
    /// false — непрерывная полоса (прежнее поведение).
    public bool Segmented { get; set; }
    public int SegmentCount { get; set; } = 14;
    /// true — вертикальные бары «живой волны» с историей пиков (панель записи).
    public bool VerticalBars { get; set; }

    public LevelMeter()
    {
        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
        _scrollTimer.Tick += (_, _) => OnScrollTick();
    }

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
        StopScroll();
    }

    /// Колбэк захвата (не UI-поток): только агрегирует пик и запускает скролл.
    /// Продвижение кольца и отрисовка — строго в тике таймера.
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
            if (peak > _pendingPeak) _pendingPeak = peak;
            if (!_scrollTimer.IsEnabled) _scrollTimer.Start();
        }));
    }

    /// Один тик скролла: пишем в кольцо агрегированный пик (или 0, если сэмплов
    /// нет — запись окончена или тишина). След уплывает влево; когда кольцо
    /// целиком ниже порога — останавливаем таймер (анимация прервана).
    private void OnScrollTick()
    {
        float value = _pendingPeak;
        _pendingPeak = 0f;

        _level = Math.Max(value, _level * 0.80f);
        _bars[_barIndex] = value;
        _barIndex = (_barIndex + 1) % BarCount;
        InvalidateVisual();

        if (value < BarEpsilon && IsRingSilent()) StopScroll();
    }

    private bool IsRingSilent()
    {
        for (int i = 0; i < BarCount; i++)
        {
            if (_bars[i] >= BarEpsilon) return false;
        }
        return true;
    }

    private void StopScroll()
    {
        _pendingPeak = 0f;
        _scrollTimer.Stop();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(new Point(0, 0), RenderSize);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        if (VerticalBars)
        {
            RenderBars(dc, bounds);
            return;
        }
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

    /// Вертикальные бары волны: история пиков по кольцу, как на моке Murmur.
    /// Компактный «pill»-вид: скруглённые торцы, плотнее шаг, вертикальный
    /// акцентный градиент у горящих баров; в простое — ровная dotted-линия.
    private void RenderBars(DrawingContext dc, Rect bounds)
    {
        var litBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1)
        };
        litBrush.GradientStops.Add(new GradientStop(Color.FromRgb(124, 245, 227), 0.0));  // #7CF5E3
        litBrush.GradientStops.Add(new GradientStop(Color.FromRgb(45, 212, 191), 0.55));  // #2DD4BF
        litBrush.GradientStops.Add(new GradientStop(Color.FromRgb(15, 118, 110), 1.0));   // #0F766E
        var dimBrush = new SolidColorBrush(Color.FromRgb(24, 36, 38));    // #182426
        double slot = bounds.Width / BarCount;
        double barWidth = Math.Max(2, slot * 0.6);
        double radius = barWidth / 2;
        for (int i = 0; i < BarCount; i++)
        {
            float v = _bars[(_barIndex + i) % BarCount];
            double h = Math.Max(4, bounds.Height * Math.Clamp(v * 1.4f, 0f, 1f));
            double x = i * slot + (slot - barWidth) / 2;
            double y = (bounds.Height - h) / 2;
            dc.DrawRoundedRectangle(v > BarEpsilon ? litBrush : dimBrush, null, new Rect(x, y, barWidth, h), radius, radius);
        }
    }
}