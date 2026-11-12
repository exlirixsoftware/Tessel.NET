using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Tessel.NET.Controls;

/// <summary>
/// A slider with two thumbs that selects a range: <see cref="LowerValue"/> to <see cref="UpperValue"/> between
/// <see cref="Minimum"/> and <see cref="Maximum"/> (a price filter, a trim range, a time window). The thumbs never cross, and
/// <see cref="MinimumRange"/> keeps them apart. Click the track to move the nearest thumb; the arrow keys, Page Up/Down, Home and End
/// move the focused thumb.
/// </summary>
[TemplatePart(Name = "PART_Canvas", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_Fill", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_LowerThumb", Type = typeof(Thumb))]
[TemplatePart(Name = "PART_UpperThumb", Type = typeof(Thumb))]
public class RangeSlider : Control
{
    private Canvas? _canvas;
    private FrameworkElement? _fill;
    private Thumb? _lower;
    private Thumb? _upper;
    private double _dragStart;
    private double _dragTotal;

    static RangeSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RangeSlider), new FrameworkPropertyMetadata(typeof(RangeSlider)));
    }

    #region Dependency properties

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(RangeSlider),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsArrange, OnBoundsChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(RangeSlider),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsArrange, OnBoundsChanged));

    public static readonly DependencyProperty LowerValueProperty = DependencyProperty.Register(
        nameof(LowerValue), typeof(double), typeof(RangeSlider),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceLower));

    public static readonly DependencyProperty UpperValueProperty = DependencyProperty.Register(
        nameof(UpperValue), typeof(double), typeof(RangeSlider),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceUpper));

    public static readonly DependencyProperty MinimumRangeProperty = DependencyProperty.Register(
        nameof(MinimumRange), typeof(double), typeof(RangeSlider),
        new PropertyMetadata(0d, OnBoundsChanged, (_, v) => Math.Max(0d, (double)v)));

    public static readonly DependencyProperty SnapIntervalProperty = DependencyProperty.Register(
        nameof(SnapInterval), typeof(double), typeof(RangeSlider),
        new PropertyMetadata(0d, OnBoundsChanged, (_, v) => Math.Max(0d, (double)v)));

    public static readonly DependencyProperty SmallChangeProperty = DependencyProperty.Register(
        nameof(SmallChange), typeof(double), typeof(RangeSlider), new PropertyMetadata(1d));

    public static readonly DependencyProperty LargeChangeProperty = DependencyProperty.Register(
        nameof(LargeChange), typeof(double), typeof(RangeSlider), new PropertyMetadata(10d));

    public static readonly DependencyProperty StringFormatProperty = DependencyProperty.Register(
        nameof(StringFormat), typeof(string), typeof(RangeSlider), new PropertyMetadata("0.##", (d, _) => ((RangeSlider)d).UpdateLayout2()));

    public static readonly RoutedEvent RangeChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(RangeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RangeSlider));

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

    /// <summary>The start of the selected range.</summary>
    public double LowerValue
    {
        get => (double)GetValue(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    /// <summary>The end of the selected range.</summary>
    public double UpperValue
    {
        get => (double)GetValue(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    /// <summary>The smallest distance allowed between the thumbs (0 lets them meet).</summary>
    public double MinimumRange
    {
        get => (double)GetValue(MinimumRangeProperty);
        set => SetValue(MinimumRangeProperty, value);
    }

    /// <summary>Values snap to multiples of this step, counted from <see cref="Minimum"/> (0 = continuous).</summary>
    public double SnapInterval
    {
        get => (double)GetValue(SnapIntervalProperty);
        set => SetValue(SnapIntervalProperty, value);
    }

    /// <summary>The step of the arrow keys (the <see cref="SnapInterval"/> wins when it is set).</summary>
    public double SmallChange
    {
        get => (double)GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    /// <summary>The step of Page Up and Page Down.</summary>
    public double LargeChange
    {
        get => (double)GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    /// <summary>Formats the value shown in the tooltip of each thumb.</summary>
    public string StringFormat
    {
        get => (string)GetValue(StringFormatProperty);
        set => SetValue(StringFormatProperty, value);
    }

    /// <summary>Raised when <see cref="LowerValue"/> or <see cref="UpperValue"/> changes.</summary>
    public event RoutedEventHandler RangeChanged
    {
        add => AddHandler(RangeChangedEvent, value);
        remove => RemoveHandler(RangeChangedEvent, value);
    }

    #endregion

    #region Coercion

    private static void OnBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var slider = (RangeSlider)d;
        slider.CoerceValue(UpperValueProperty);
        slider.CoerceValue(LowerValueProperty);
        slider.UpdateLayout2();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var slider = (RangeSlider)d;
        if (e.Property == LowerValueProperty) slider.CoerceValue(UpperValueProperty);
        else slider.CoerceValue(LowerValueProperty);
        slider.UpdateLayout2();
        slider.RaiseEvent(new RoutedEventArgs(RangeChangedEvent));
    }

    private static object CoerceLower(DependencyObject d, object baseValue)
    {
        var s = (RangeSlider)d;
        var max = Math.Max(s.Minimum, s.UpperValue - s.MinimumRange);
        return Math.Clamp(s.Snap((double)baseValue), s.Minimum, max);
    }

    private static object CoerceUpper(DependencyObject d, object baseValue)
    {
        var s = (RangeSlider)d;
        var min = Math.Min(s.Maximum, s.LowerValue + s.MinimumRange);
        return Math.Clamp(s.Snap((double)baseValue), min, Math.Max(s.Maximum, min));
    }

    private double Snap(double value)
    {
        var step = SnapInterval;
        if (step <= 0) return value;
        return Math.Round((value - Minimum) / step) * step + Minimum;
    }

    #endregion

    #region Template and layout

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_canvas != null) _canvas.SizeChanged -= OnCanvasSizeChanged;

        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
        _fill = GetTemplateChild("PART_Fill") as FrameworkElement;
        _lower = GetTemplateChild("PART_LowerThumb") as Thumb;
        _upper = GetTemplateChild("PART_UpperThumb") as Thumb;

        if (_canvas != null) _canvas.SizeChanged += OnCanvasSizeChanged;
        Attach(_lower, true);
        Attach(_upper, false);
        UpdateLayout2();
    }

    private void Attach(Thumb? thumb, bool lower)
    {
        if (thumb == null) return;
        thumb.Focusable = true;
        thumb.DragStarted += (_, _) =>
        {
            thumb.Focus();
            _dragStart = lower ? LowerValue : UpperValue;
            _dragTotal = 0;
        };
        thumb.DragDelta += (_, e) =>
        {
            var usable = Usable;
            if (usable <= 0) return;
            _dragTotal += e.HorizontalChange;
            Set(lower, _dragStart + _dragTotal / usable * (Maximum - Minimum));
        };
        thumb.PreviewKeyDown += (_, e) => OnThumbKey(lower, e);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => UpdateLayout2();

    private double ThumbWidth => _lower == null ? 0 : _lower.ActualWidth > 0 ? _lower.ActualWidth : _lower.Width;

    private double Usable => _canvas == null ? 0 : Math.Max(0, _canvas.ActualWidth - ThumbWidth);

    private void Set(bool lower, double value)
    {
        if (lower) LowerValue = value;
        else UpperValue = value;
    }

    private void UpdateLayout2()
    {
        if (_canvas == null || _lower == null || _upper == null || _fill == null) return;

        var width = ThumbWidth;
        var usable = Usable;
        var range = Maximum - Minimum;
        double At(double v) => range <= 0 ? 0 : (v - Minimum) / range * usable;

        var lowerX = At(LowerValue);
        var upperX = At(UpperValue);
        Canvas.SetLeft(_lower, lowerX);
        Canvas.SetLeft(_upper, upperX);
        Canvas.SetLeft(_fill, lowerX + width / 2);
        _fill.Width = Math.Max(0, upperX - lowerX);

        // at the right end the lower thumb must stay on top, or it could not be dragged back
        Panel.SetZIndex(_lower, LowerValue > Minimum + range / 2 ? 2 : 0);
        Panel.SetZIndex(_upper, 1);

        var format = string.IsNullOrEmpty(StringFormat) ? "0.##" : StringFormat;
        _lower.ToolTip = LowerValue.ToString(format, CultureInfo.CurrentCulture);
        _upper.ToolTip = UpperValue.ToString(format, CultureInfo.CurrentCulture);
    }

    #endregion

    #region Input

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_canvas == null || _lower == null || _upper == null || e.Handled) return;
        if (e.OriginalSource is DependencyObject source && (IsInside(source, _lower) || IsInside(source, _upper))) return;

        var usable = Usable;
        if (usable <= 0) return;
        var x = e.GetPosition(_canvas).X - ThumbWidth / 2;
        var value = Minimum + Math.Clamp(x / usable, 0, 1) * (Maximum - Minimum);

        var lower = Math.Abs(value - LowerValue) <= Math.Abs(value - UpperValue);
        if (LowerValue == UpperValue) lower = value < LowerValue;
        Set(lower, value);
        (lower ? _lower : _upper).Focus();
        e.Handled = true;
    }

    private static bool IsInside(DependencyObject element, DependencyObject ancestor)
    {
        for (var current = element; current != null; current = System.Windows.Media.VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor)) return true;
        }
        return false;
    }

    private void OnThumbKey(bool lower, KeyEventArgs e)
    {
        var small = SnapInterval > 0 ? SnapInterval : SmallChange;
        var large = Math.Max(LargeChange, small);
        var current = lower ? LowerValue : UpperValue;

        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                Set(lower, current - small);
                break;
            case Key.Right:
            case Key.Up:
                Set(lower, current + small);
                break;
            case Key.PageDown:
                Set(lower, current - large);
                break;
            case Key.PageUp:
                Set(lower, current + large);
                break;
            case Key.Home:
                Set(lower, lower ? Minimum : LowerValue + MinimumRange);
                break;
            case Key.End:
                Set(lower, lower ? UpperValue - MinimumRange : Maximum);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    #endregion
}
