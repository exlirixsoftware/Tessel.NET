using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Tessel.NET.Controls;

/// <summary>A colored stretch of a <see cref="Gauge"/> scale (for example 80 to 100 in red). The brush can be a theme resource.</summary>
public sealed class GaugeRange : Freezable
{
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(double), typeof(GaugeRange), new PropertyMetadata(0d));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(double), typeof(GaugeRange), new PropertyMetadata(0d));

    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush), typeof(Brush), typeof(GaugeRange), new PropertyMetadata(null));

    public double From
    {
        get => (double)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public double To
    {
        get => (double)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new GaugeRange();
}

/// <summary>
/// A circular meter for a value between <see cref="Minimum"/> and <see cref="Maximum"/>: CPU load, a score, a battery. <see cref="Ranges"/>
/// add colored zones; the arc takes the color of the zone the value is in. The arc animates to a new value.
/// <code>&lt;tessel:Gauge Value="72" Unit="%" Header="CPU"&gt;&lt;tessel:GaugeRange From="80" To="100" Brush="Red" /&gt;&lt;/tessel:Gauge&gt;</code>
/// </summary>
[ContentProperty(nameof(Ranges))]
public class Gauge : Control
{
    static Gauge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Gauge), new FrameworkPropertyMetadata(typeof(Gauge)));
        FocusableProperty.OverrideMetadata(typeof(Gauge), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Gauge), new FrameworkPropertyMetadata(false));
    }

    public Gauge()
    {
        Ranges.Changed += (_, _) => InvalidateVisual();
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(Gauge),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Gauge)d).OnValueChanged()));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(Gauge), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Gauge)d).OnValueChanged()));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Gauge), new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Gauge)d).OnValueChanged()));

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(Gauge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(Gauge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StringFormatProperty = DependencyProperty.Register(
        nameof(StringFormat), typeof(string), typeof(Gauge), new FrameworkPropertyMetadata("0.#", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(Gauge), new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(Gauge), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowValueProperty = DependencyProperty.Register(
        nameof(ShowValue), typeof(bool), typeof(Gauge), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StartAngleProperty = DependencyProperty.Register(
        nameof(StartAngle), typeof(double), typeof(Gauge), new FrameworkPropertyMetadata(135d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SweepAngleProperty = DependencyProperty.Register(
        nameof(SweepAngle), typeof(double), typeof(Gauge),
        new FrameworkPropertyMetadata(270d, FrameworkPropertyMetadataOptions.AffectsRender, null, (_, v) => Math.Clamp((double)v, 10, 359)));

    public static readonly DependencyProperty ValueBrushProperty = DependencyProperty.Register(
        nameof(ValueBrush), typeof(Brush), typeof(Gauge), new FrameworkPropertyMetadata(Brushes.RoyalBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(Gauge), new FrameworkPropertyMetadata(Brushes.Gainsboro, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(double), typeof(Gauge), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The value shown (the arc moves to it).</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>A label under the value ("CPU").</summary>
    public string? Header
    {
        get => (string?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>A short text after the value ("%", "°C").</summary>
    public string? Unit
    {
        get => (string?)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public string StringFormat
    {
        get => (string)GetValue(StringFormatProperty);
        set => SetValue(StringFormatProperty, value);
    }

    /// <summary>The width of the arc.</summary>
    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>Moves the arc smoothly when the value changes.</summary>
    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    public bool ShowValue
    {
        get => (bool)GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    /// <summary>Where the scale starts, in degrees clockwise from the right (135 = lower left).</summary>
    public double StartAngle
    {
        get => (double)GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    /// <summary>How far the scale goes round, in degrees (270 by default; 180 makes a half circle).</summary>
    public double SweepAngle
    {
        get => (double)GetValue(SweepAngleProperty);
        set => SetValue(SweepAngleProperty, value);
    }

    /// <summary>The color of the arc when the value is in none of the <see cref="Ranges"/>.</summary>
    public Brush ValueBrush
    {
        get => (Brush)GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <summary>The value the arc is drawn at right now (it follows <see cref="Value"/> while animating).</summary>
    internal double DisplayValue => (double)GetValue(DisplayValueProperty);

    /// <summary>Colored zones of the scale.</summary>
    public FreezableCollection<GaugeRange> Ranges { get; } = [];

    /// <summary>The color the arc has at <paramref name="value"/>.</summary>
    public Brush BrushAt(double value)
    {
        foreach (var range in Ranges)
        {
            if (range.Brush != null && value >= range.From && value <= range.To) return range.Brush;
        }
        return ValueBrush;
    }

    private double Fraction(double value)
    {
        var span = Maximum - Minimum;
        return span <= 0 ? 0 : Math.Clamp((value - Minimum) / span, 0, 1);
    }

    private void OnValueChanged()
    {
        if (IsAnimated && IsLoaded && IsVisible)
        {
            var animation = new DoubleAnimation(Math.Clamp(Value, Math.Min(Minimum, Maximum), Math.Max(Minimum, Maximum)), TimeSpan.FromMilliseconds(380))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            BeginAnimation(DisplayValueProperty, animation);
        }
        else
        {
            BeginAnimation(DisplayValueProperty, null);
            SetValue(DisplayValueProperty, Value);
        }
        InvalidateVisual();
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        SetValue(DisplayValueProperty, Value);
    }

    private static Point OnCircle(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private static Geometry Arc(Point center, double radius, double start, double sweep)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(OnCircle(center, radius, start), false, false);
            ctx.ArcTo(OnCircle(center, radius, start + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private FormattedText Text(string text, Brush brush, double size, bool bold = false)
    {
        var typeface = new Typeface(FontFamily, FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
        return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var side = Math.Min(ActualWidth, ActualHeight);
        if (side < 24) return;

        var thickness = Math.Min(Thickness, side / 4);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = side / 2 - thickness / 2 - 2;
        var start = StartAngle;
        var sweep = SweepAngle;

        var track = new Pen(TrackBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, track, Arc(center, radius, start, sweep));

        // the zones of the scale as a thin ring inside the arc
        if (Ranges.Count > 0)
        {
            var inner = radius - thickness / 2 - 5;
            foreach (var range in Ranges)
            {
                if (range.Brush == null || inner < 8) continue;
                var from = Fraction(range.From);
                var to = Fraction(range.To);
                if (to - from <= 0) continue;
                dc.DrawGeometry(null, new Pen(range.Brush, 3), Arc(center, inner, start + sweep * from, sweep * (to - from)));
            }
        }

        var shown = DisplayValue;
        var fraction = Fraction(shown);
        if (fraction > 0)
        {
            var pen = new Pen(BrushAt(Value), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, pen, Arc(center, radius, start, Math.Max(sweep * fraction, 0.5)));
        }

        var foreground = Foreground ?? Brushes.Black;
        var format = string.IsNullOrEmpty(StringFormat) ? "0.#" : StringFormat;
        var valueSize = Math.Max(10, radius * 0.56);
        var y = center.Y - valueSize * 0.62;
        if (ShowValue)
        {
            var value = Text(shown.ToString(format, CultureInfo.CurrentCulture), foreground, valueSize, true);
            var unit = string.IsNullOrEmpty(Unit) ? null : Text(Unit, Foreground ?? Brushes.Gray, valueSize * 0.42);
            var total = value.Width + (unit?.Width ?? 0) + (unit != null ? 2 : 0);
            var x = center.X - total / 2;
            dc.DrawText(value, new Point(x, y));
            if (unit != null) dc.DrawText(unit, new Point(x + value.Width + 2, y + value.Height - unit.Height - valueSize * 0.08));
        }
        if (!string.IsNullOrEmpty(Header) && radius > 30)
        {
            var header = Text(Header, Foreground ?? Brushes.Gray, Math.Max(9, valueSize * 0.34));
            dc.PushOpacity(0.75);
            dc.DrawText(header, new Point(center.X - header.Width / 2, y + valueSize * 1.28));
            dc.Pop();
        }
    }
}
