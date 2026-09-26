using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Tessel.UI.Controls;

/// <summary>An indeterminate, spinning progress indicator.</summary>
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

    private void OnArcSizeChanged(object sender, SizeChangedEventArgs e) => UpdateDash();

    private void UpdateDash()
    {
        if (_arc == null || Thickness <= 0) return;
        var diameter = Math.Min(_arc.ActualWidth, _arc.ActualHeight) - Thickness;
        if (diameter <= 0) return;

        // Dash lengths are expressed in multiples of the stroke thickness.
        var circumference = Math.PI * diameter / Thickness;
        _arc.StrokeDashArray = new DoubleCollection { circumference * 0.3, circumference };
    }
}
