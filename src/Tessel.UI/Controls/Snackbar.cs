using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Tessel.UI.Helpers;

namespace Tessel.UI.Controls;

/// <summary>
/// Shows transient notifications (toasts) stacked in the bottom-right corner of a window.
/// <code>Snackbar.Show("Saved", "Your changes were saved.", InfoBarSeverity.Success);</code>
/// </summary>
public static class Snackbar
{
    private static readonly ConditionalWeakTable<Window, StackPanel> Hosts = new();

    /// <summary>Default time a snackbar stays visible.</summary>
    public static TimeSpan DefaultDuration { get; set; } = TimeSpan.FromSeconds(4);

    public static InfoBar? Show(
        string title,
        string? message = null,
        InfoBarSeverity severity = InfoBarSeverity.Informational,
        TimeSpan? duration = null,
        Window? owner = null)
    {
        var window = OverlayHost.ResolveOwner(owner);
        if (window == null) return null;

        var host = GetHost(window);
        if (host == null) return null;

        var bar = new InfoBar
        {
            Title = title,
            Message = message ?? string.Empty,
            Severity = severity,
            Width = 380,
            Margin = new Thickness(0, 8, 0, 0),
        };
        bar.SetResourceReference(Control.BackgroundProperty, "Tessel.SurfaceBrush");
        bar.SetResourceReference(UIElement.EffectProperty, "Tessel.Shadow");
        bar.Closed += (_, _) => host.Children.Remove(bar);

        host.Children.Add(bar);
        Animate(bar, fromOpacity: 0, toOpacity: 1, fromY: 16, toY: 0, completed: null);

        var timer = new DispatcherTimer { Interval = duration ?? DefaultDuration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (bar.IsMouseOver)
            {
                timer.Start();
                return;
            }
            Animate(bar, 1, 0, 0, 8, () => bar.IsOpen = false);
        };
        timer.Start();

        return bar;
    }

    private static StackPanel? GetHost(Window window)
    {
        if (Hosts.TryGetValue(window, out var existing)) return existing;

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(24),
        };
        var root = new Grid { Children = { stack } };

        if (OverlayHost.Show(window, root) == null) return null;
        Hosts.Add(window, stack);
        return stack;
    }

    private static void Animate(FrameworkElement element, double fromOpacity, double toOpacity, double fromY, double toY, Action? completed)
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(200));
        var translate = new TranslateTransform();
        element.RenderTransform = translate;

        var opacity = new DoubleAnimation(fromOpacity, toOpacity, duration) { EasingFunction = new CubicEase() };
        if (completed != null) opacity.Completed += (_, _) => completed();

        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, toY, duration) { EasingFunction = new CubicEase() });
        element.BeginAnimation(UIElement.OpacityProperty, opacity);
    }
}
