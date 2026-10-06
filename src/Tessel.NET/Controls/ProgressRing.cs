using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Tessel.NET.Controls;

/// <summary>A circular progress indicator: spinning when <see cref="IsIndeterminate"/>, otherwise showing <see cref="Value"/>.</summary>
[TemplatePart(Name = "PART_Arc", Type = typeof(Ellipse))]
public class ProgressRing : Control
{
    private Ellipse? _arc;

    static ProgressRing()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ProgressRing), new FrameworkPropertyMetadata(typeof(ProgressRing)));
        FocusableProperty.OverrideMetadata(typeof(ProgressRing), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(ProgressRing), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(ProgressRing), new PropertyMetadata(true));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Spins endlessly (default). Set to false to show <see cref="Value"/> as a progress arc.</summary>
    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
        nameof(IsIndeterminate), typeof(bool), typeof(ProgressRing),
        new PropertyMetadata(true, (d, _) => ((ProgressRing)d).UpdateDash()));

    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ProgressRing),
        new PropertyMetadata(0d, (d, _) => ((ProgressRing)d).UpdateDash()));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(ProgressRing),
        new PropertyMetadata(0d, (d, _) => ((ProgressRing)d).UpdateDash()));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(ProgressRing),
        new PropertyMetadata(100d, (d, _) => ((ProgressRing)d).UpdateDash()));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(ProgressRing),
        new PropertyMetadata(3d, (d, _) => ((ProgressRing)d).UpdateDash()));

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (_arc != null) _arc.SizeChanged -= OnArcSizeChanged;
        base.OnApplyTemplate();
        _arc = GetTemplateChild("PART_Arc") as Ellipse;
        if (_arc != null) _arc.SizeChanged += OnArcSizeChanged;
        UpdateDash();
    }

    private double Fraction()
    {
        var range = Maximum - Minimum;
        return range <= 0 ? 0 : Math.Clamp((Value - Minimum) / range, 0, 1);
    }

    private void OnArcSizeChanged(object sender, SizeChangedEventArgs e) => UpdateDash();

    private void UpdateDash()
    {
        if (_arc == null || Thickness <= 0) return;
        var diameter = Math.Min(_arc.ActualWidth, _arc.ActualHeight) - Thickness;
        if (diameter <= 0) return;

        // Dash lengths are expressed in multiples of the stroke thickness.
        var circumference = Math.PI * diameter / Thickness;
        var fraction = IsIndeterminate ? 0.3 : Fraction();
        _arc.StrokeDashArray = new DoubleCollection { circumference * fraction, circumference };
    }
}
