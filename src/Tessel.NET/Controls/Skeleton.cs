using System.Windows;
using System.Windows.Controls;

namespace Tessel.NET.Controls;

/// <summary>
/// A shimmering placeholder shown while content loads. Size it like the content it stands in for,
/// or use <see cref="IsCircle"/> for avatars.
/// </summary>
public class Skeleton : Control
{
    static Skeleton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Skeleton), new FrameworkPropertyMetadata(typeof(Skeleton)));
        FocusableProperty.OverrideMetadata(typeof(Skeleton), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Skeleton), new FrameworkPropertyMetadata(false));
    }

    /// <summary>Runs the shimmer animation (default true).</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(Skeleton), new PropertyMetadata(true));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(Skeleton), new PropertyMetadata(new CornerRadius(4)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Draws an ellipse instead of a rounded rectangle (use equal width and height for a circle).</summary>
    public static readonly DependencyProperty IsCircleProperty = DependencyProperty.Register(
        nameof(IsCircle), typeof(bool), typeof(Skeleton), new PropertyMetadata(false));

    public bool IsCircle
    {
        get => (bool)GetValue(IsCircleProperty);
        set => SetValue(IsCircleProperty, value);
    }
}
