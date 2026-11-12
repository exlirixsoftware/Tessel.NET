using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tessel.NET.Controls;

/// <summary>How a <see cref="Sparkline"/> draws its values.</summary>
public enum SparklineKind
{
    /// <summary>A line through the values.</summary>
    Line,

    /// <summary>A line with the area below it filled.</summary>
    Area,

    /// <summary>One bar per value, from zero; negative values use <see cref="Sparkline.NegativeBrush"/>.</summary>
    Bars,
}

/// <summary>
/// A tiny chart without axes for dashboards, table cells and cards: the trend of a series at a glance. Bind <see cref="Values"/> to any
/// list of numbers (an <c>ObservableCollection&lt;double&gt;</c> redraws it as it grows).
/// </summary>
public class Sparkline : Control
{
    private const double Inset = 3;

    private INotifyCollectionChanged? _observed;

    static Sparkline()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Sparkline), new FrameworkPropertyMetadata(typeof(Sparkline)));
        FocusableProperty.OverrideMetadata(typeof(Sparkline), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Sparkline), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((Sparkline)d).OnValuesChanged((IEnumerable?)e.NewValue)));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(SparklineKind), typeof(Sparkline), new FrameworkPropertyMetadata(SparklineKind.Line, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(Brushes.RoyalBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty NegativeBrushProperty = DependencyProperty.Register(
        nameof(NegativeBrush), typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(Brushes.IndianRed, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(1.75, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowLastPointProperty = DependencyProperty.Register(
        nameof(ShowLastPoint), typeof(bool), typeof(Sparkline), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowExtremesProperty = DependencyProperty.Register(
        nameof(ShowExtremes), typeof(bool), typeof(Sparkline), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsSmoothProperty = DependencyProperty.Register(
        nameof(IsSmooth), typeof(bool), typeof(Sparkline), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The numbers to draw, in order (any list of double, int, float or decimal).</summary>
    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public SparklineKind Kind
    {
        get => (SparklineKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>The color of the line, the area and the bars.</summary>
    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>The color of bars below zero (defaults to a red).</summary>
    public Brush NegativeBrush
    {
        get => (Brush)GetValue(NegativeBrushProperty);
        set => SetValue(NegativeBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>The value at the bottom (NaN = the smallest value).</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>The value at the top (NaN = the largest value).</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Marks the last value with a dot.</summary>
    public bool ShowLastPoint
    {
        get => (bool)GetValue(ShowLastPointProperty);
        set => SetValue(ShowLastPointProperty, value);
    }

    /// <summary>Marks the highest and the lowest value with dots (lines and areas).</summary>
    public bool ShowExtremes
    {
        get => (bool)GetValue(ShowExtremesProperty);
        set => SetValue(ShowExtremesProperty, value);
    }

    /// <summary>Rounds the corners of the line.</summary>
    public bool IsSmooth
    {
        get => (bool)GetValue(IsSmoothProperty);
        set => SetValue(IsSmoothProperty, value);
    }

    private void OnValuesChanged(IEnumerable? values)
    {
        if (_observed != null) _observed.CollectionChanged -= OnCollectionChanged;
        _observed = values as INotifyCollectionChanged;
        if (_observed != null) _observed.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    /// <summary>The values as numbers; anything that is not a number is skipped.</summary>
    internal List<double> Numbers()
    {
        var list = new List<double>();
        if (Values == null) return list;
        foreach (var item in Values)
        {
            if (item == null) continue;
            try
            {
                var number = Convert.ToDouble(item, System.Globalization.CultureInfo.InvariantCulture);
                if (!double.IsNaN(number) && !double.IsInfinity(number)) list.Add(number);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                // not a number
            }
        }
        return list;
    }

    /// <summary>The path through the points; closed down to the baseline when it is used as the filled area.</summary>
    private Geometry BuildPath(List<Point> points, double height, bool area)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (area)
            {
                ctx.BeginFigure(new Point(points[0].X, height - Inset), true, true);
                ctx.LineTo(points[0], false, false);
            }
            else
            {
                ctx.BeginFigure(points[0], false, false);
            }

            var stroked = !area;
            if (IsSmooth && points.Count > 2)
            {
                for (var i = 0; i < points.Count - 1; i++)
                {
                    var p0 = points[Math.Max(i - 1, 0)];
                    var p1 = points[i];
                    var p2 = points[i + 1];
                    var p3 = points[Math.Min(i + 2, points.Count - 1)];
                    ctx.BezierTo(
                        new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6),
                        new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6),
                        p2, stroked, stroked);
                }
            }
            else
            {
                for (var i = 1; i < points.Count; i++) ctx.LineTo(points[i], stroked, stroked);
            }

            if (area) ctx.LineTo(new Point(points[^1].X, height - Inset), false, false);
        }
        geometry.Freeze();
        return geometry;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = ActualWidth;
        var height = ActualHeight;
        var values = Numbers();
        if (values.Count == 0 || width < 8 || height < 8) return;

        var bars = Kind == SparklineKind.Bars;
        var min = double.IsNaN(Minimum) ? values.Min() : Minimum;
        var max = double.IsNaN(Maximum) ? values.Max() : Maximum;
        if (bars)
        {
            min = Math.Min(min, 0);
            max = Math.Max(max, 0);
        }
        if (max - min < 1e-12)
        {
            max = min + 1;
            min -= 1;
        }

        var innerWidth = width - 2 * Inset;
        var innerHeight = height - 2 * Inset;
        double Y(double value) => height - Inset - (Math.Clamp(value, min, max) - min) / (max - min) * innerHeight;

        if (bars)
        {
            var slot = innerWidth / values.Count;
            var barWidth = Math.Max(1, slot * 0.72);
            var zero = Y(0);
            for (var i = 0; i < values.Count; i++)
            {
                var top = Y(values[i]);
                var x = Inset + (i + 0.5) * slot - barWidth / 2;
                var rect = new Rect(x, Math.Min(top, zero), barWidth, Math.Max(1, Math.Abs(zero - top)));
                dc.DrawRoundedRectangle(values[i] < 0 ? NegativeBrush : Stroke, null, rect, Math.Min(2, barWidth / 2), Math.Min(2, barWidth / 2));
            }
            return;
        }

        double X(int i) => values.Count == 1 ? width / 2 : Inset + i * innerWidth / (values.Count - 1);
        var points = values.Select((v, i) => new Point(X(i), Y(v))).ToList();

        var line = BuildPath(points, height, false);
        if (Kind == SparklineKind.Area && points.Count > 1)
        {
            dc.PushOpacity(0.2);
            dc.DrawGeometry(Stroke, null, BuildPath(points, height, true));
            dc.Pop();
        }

        dc.DrawGeometry(null, new Pen(Stroke, StrokeThickness) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, line);

        var ring = new Pen(Background ?? Brushes.White, 1.5);
        if (ShowExtremes && points.Count > 2)
        {
            var low = values.IndexOf(values.Min());
            var high = values.IndexOf(values.Max());
            dc.DrawEllipse(Stroke, ring, points[low], 2.75, 2.75);
            dc.DrawEllipse(Stroke, ring, points[high], 2.75, 2.75);
        }
        if (ShowLastPoint) dc.DrawEllipse(Stroke, ring, points[^1], 3.25, 3.25);
    }
}
