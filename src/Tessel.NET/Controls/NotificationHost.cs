using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tessel.NET.Helpers;

namespace Tessel.NET.Controls;

/// <summary>Where a <see cref="NotificationHost"/> stacks its notifications.</summary>
public enum NotificationPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

/// <summary>Why a <see cref="Notification"/> went away.</summary>
public enum NotificationCloseReason
{
    /// <summary>Its time ran out.</summary>
    Timeout,

    /// <summary>The user clicked the close button.</summary>
    User,

    /// <summary>The user clicked the action button.</summary>
    Action,

    /// <summary><see cref="Notification.Close"/> or <see cref="NotificationHost.Clear"/> was called.</summary>
    Programmatic,
}

public sealed class NotificationClosedEventArgs(Notification notification, NotificationCloseReason reason) : EventArgs
{
    public Notification Notification { get; } = notification;

    public NotificationCloseReason Reason { get; } = reason;
}

/// <summary>
/// A notification shown by a <see cref="NotificationHost"/>. Keep the object returned by <see cref="NotificationHost.Show(string, string?, InfoBarSeverity, TimeSpan?, string?, Action?)"/>
/// to change its text while it is visible ("Uploading 40%...") or to close it.
/// </summary>
public sealed class Notification
{
    private string _title;
    private string _message;
    private InfoBarSeverity _severity;

    internal Notification(NotificationHost host, string title, string message, InfoBarSeverity severity, TimeSpan duration, string? actionText, Action? action)
    {
        Host = host;
        _title = title;
        _message = message;
        _severity = severity;
        Duration = duration;
        ActionText = actionText;
        Action = action;
    }

    internal NotificationHost Host { get; }

    internal InfoBar? Bar { get; set; }

    internal FrameworkElement? Container { get; set; }

    internal AnimationClock? Clock { get; set; }

    internal Action? Action { get; }

    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            if (Bar != null) Bar.Title = _title;
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            _message = value ?? string.Empty;
            if (Bar != null) Bar.Message = _message;
        }
    }

    public InfoBarSeverity Severity
    {
        get => _severity;
        set
        {
            _severity = value;
            if (Bar != null) Bar.Severity = value;
        }
    }

    /// <summary>How long the notification stays (zero or <see cref="Timeout.InfiniteTimeSpan"/>: until it is closed).</summary>
    public TimeSpan Duration { get; }

    public string? ActionText { get; }

    /// <summary>True once the notification has been closed.</summary>
    public bool IsClosed { get; internal set; }

    /// <summary>Raised once, when the notification is closed.</summary>
    public event EventHandler<NotificationClosedEventArgs>? Closed;

    public void Close() => Host.Close(this, NotificationCloseReason.Programmatic);

    internal void RaiseClosed(NotificationCloseReason reason) => Closed?.Invoke(this, new NotificationClosedEventArgs(this, reason));
}

/// <summary>
/// Stacks notifications (toasts) in a corner of the layout it is placed in. Put it last in the root grid so it draws above the page, or let
/// <see cref="Attach"/> do that for a window. At most <see cref="MaxVisible"/> are shown; the rest wait in a queue. A bar under each one counts
/// down its time and pauses while the mouse is over it; a notification can have an action button.
/// <code>Toasts.Show("Saved", "3 files were uploaded.", InfoBarSeverity.Success, actionText: "Undo", action: Undo);</code>
/// </summary>
[TemplatePart(Name = "PART_Panel", Type = typeof(StackPanel))]
public class NotificationHost : Control
{
    private static readonly ConditionalWeakTable<Window, NotificationHost> Attached = new();

    private readonly List<Notification> _visible = [];
    private readonly List<Notification> _pending = [];
    private StackPanel? _panel;

    static NotificationHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NotificationHost), new FrameworkPropertyMetadata(typeof(NotificationHost)));
    }

    #region Dependency properties

    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position), typeof(NotificationPosition), typeof(NotificationHost),
        new PropertyMetadata(NotificationPosition.BottomRight, (d, _) => ((NotificationHost)d).UpdateAlignment()));

    public static readonly DependencyProperty MaxVisibleProperty = DependencyProperty.Register(
        nameof(MaxVisible), typeof(int), typeof(NotificationHost),
        new PropertyMetadata(5, (d, _) => ((NotificationHost)d).PresentPending(), (_, v) => Math.Max(1, (int)v)));

    public static readonly DependencyProperty DefaultDurationProperty = DependencyProperty.Register(
        nameof(DefaultDuration), typeof(TimeSpan), typeof(NotificationHost), new PropertyMetadata(TimeSpan.FromSeconds(4)));

    public static readonly DependencyProperty NotificationWidthProperty = DependencyProperty.Register(
        nameof(NotificationWidth), typeof(double), typeof(NotificationHost), new PropertyMetadata(380d));

    public static readonly DependencyProperty ShowProgressProperty = DependencyProperty.Register(
        nameof(ShowProgress), typeof(bool), typeof(NotificationHost), new PropertyMetadata(true));

    /// <summary>The corner (or top/bottom center) the notifications are stacked in. New ones appear nearest to the screen edge.</summary>
    public NotificationPosition Position
    {
        get => (NotificationPosition)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>How many notifications are shown at once (1 or more); the others wait until there is room.</summary>
    public int MaxVisible
    {
        get => (int)GetValue(MaxVisibleProperty);
        set => SetValue(MaxVisibleProperty, value);
    }

    /// <summary>The time a notification stays when none is given.</summary>
    public TimeSpan DefaultDuration
    {
        get => (TimeSpan)GetValue(DefaultDurationProperty);
        set => SetValue(DefaultDurationProperty, value);
    }

    /// <summary>The width of each notification.</summary>
    public double NotificationWidth
    {
        get => (double)GetValue(NotificationWidthProperty);
        set => SetValue(NotificationWidthProperty, value);
    }

    /// <summary>Shows the bar that counts down the time of each notification.</summary>
    public bool ShowProgress
    {
        get => (bool)GetValue(ShowProgressProperty);
        set => SetValue(ShowProgressProperty, value);
    }

    #endregion

    /// <summary>The number of notifications on screen.</summary>
    public int Count => _visible.Count;

    /// <summary>The number of notifications waiting for room.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Raised when any notification of this host is closed.</summary>
    public event EventHandler<NotificationClosedEventArgs>? NotificationClosed;

    /// <summary>Creates a host on top of <paramref name="window"/> (or the active window) and returns it; a window gets only one.</summary>
    public static NotificationHost? Attach(Window? window = null, NotificationPosition position = NotificationPosition.BottomRight)
    {
        window = OverlayHost.ResolveOwner(window);
        if (window == null) return null;
        if (Attached.TryGetValue(window, out var existing)) return existing;

        var host = new NotificationHost { Position = position };
        if (OverlayHost.Show(window, host) == null) return null;
        Attached.Add(window, host);
        return host;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _panel = GetTemplateChild("PART_Panel") as StackPanel;
        UpdateAlignment();
        if (_panel == null) return;

        _panel.Children.Clear();
        foreach (var notification in _visible) Build(notification);
    }

    /// <summary>Shows a notification (or queues it when <see cref="MaxVisible"/> are already shown).</summary>
    /// <param name="title">The bold first line.</param>
    /// <param name="message">The text under the title.</param>
    /// <param name="severity">Colors and icon of the notification.</param>
    /// <param name="duration">How long it stays; <see cref="DefaultDuration"/> when null; zero or <see cref="Timeout.InfiniteTimeSpan"/> keeps it until it is closed.</param>
    /// <param name="actionText">The text of an optional button; <paramref name="action"/> runs when it is clicked and the notification closes.</param>
    /// <param name="action">What the action button does.</param>
    public Notification Show(
        string title,
        string? message = null,
        InfoBarSeverity severity = InfoBarSeverity.Informational,
        TimeSpan? duration = null,
        string? actionText = null,
        Action? action = null)
    {
        var notification = new Notification(this, title ?? string.Empty, message ?? string.Empty, severity, duration ?? DefaultDuration, actionText, action);
        if (_visible.Count < MaxVisible) Present(notification);
        else _pending.Add(notification);
        return notification;
    }

    /// <summary>Closes every notification, including the queued ones.</summary>
    public void Clear()
    {
        foreach (var notification in _pending.ToArray()) Close(notification, NotificationCloseReason.Programmatic);
        foreach (var notification in _visible.ToArray()) Close(notification, NotificationCloseReason.Programmatic);
    }

    internal void Close(Notification notification, NotificationCloseReason reason)
    {
        if (notification.IsClosed) return;
        notification.IsClosed = true;

        if (!_pending.Remove(notification)) _visible.Remove(notification);
        notification.Clock?.Controller?.Remove();
        notification.Clock = null;

        var container = notification.Container;
        notification.Container = null;
        if (container != null && _panel != null)
        {
            if (reason == NotificationCloseReason.User) _panel.Children.Remove(container);
            else AnimateOut(container, () => _panel?.Children.Remove(container));
        }

        notification.RaiseClosed(reason);
        NotificationClosed?.Invoke(this, new NotificationClosedEventArgs(notification, reason));
        PresentPending();
    }

    private void PresentPending()
    {
        while (_pending.Count > 0 && _visible.Count < MaxVisible)
        {
            var next = _pending[0];
            _pending.RemoveAt(0);
            Present(next);
        }
    }

    private void Present(Notification notification)
    {
        _visible.Add(notification);
        if (_panel != null) Build(notification);
    }

    private bool IsTop => Position is NotificationPosition.TopLeft or NotificationPosition.TopCenter or NotificationPosition.TopRight;

    private void UpdateAlignment()
    {
        if (_panel == null) return;
        _panel.HorizontalAlignment = Position switch
        {
            NotificationPosition.TopLeft or NotificationPosition.BottomLeft => HorizontalAlignment.Left,
            NotificationPosition.TopCenter or NotificationPosition.BottomCenter => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Right,
        };
        _panel.VerticalAlignment = IsTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
    }

    private void Build(Notification notification)
    {
        if (_panel == null || notification.IsClosed) return;

        var bar = new InfoBar
        {
            Title = notification.Title,
            Message = notification.Message,
            Severity = notification.Severity,
            Width = NotificationWidth,
            IsClosable = true,
        };
        bar.SetResourceReference(BackgroundProperty, "Tessel.SurfaceBrush");
        bar.SetResourceReference(EffectProperty, "Tessel.Shadow");

        if (!string.IsNullOrEmpty(notification.ActionText))
        {
            var button = new Button { Content = notification.ActionText, VerticalAlignment = VerticalAlignment.Center };
            button.Click += (_, _) =>
            {
                try
                {
                    notification.Action?.Invoke();
                }
                finally
                {
                    Close(notification, NotificationCloseReason.Action);
                }
            };
            bar.Content = button;
        }
        bar.Closed += (_, _) => Close(notification, NotificationCloseReason.User);

        var scale = new ScaleTransform(1, 1);
        var progress = new Border
        {
            Height = 3,
            Margin = new Thickness(10, 0, 10, 1),
            VerticalAlignment = VerticalAlignment.Bottom,
            CornerRadius = new CornerRadius(1.5),
            Opacity = 0.55,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0, 0.5),
            RenderTransform = scale,
            Visibility = ShowProgress && IsTimed(notification) ? Visibility.Visible : Visibility.Collapsed,
        };
        progress.SetResourceReference(Border.BackgroundProperty, "Tessel.AccentBrush");

        var container = new Grid { Margin = new Thickness(0, 6, 0, 6), Children = { bar, progress } };
        notification.Bar = bar;
        notification.Container = container;

        if (IsTimed(notification))
        {
            var clock = new DoubleAnimation(1, 0, notification.Duration).CreateClock();
            clock.Completed += (_, _) => Close(notification, NotificationCloseReason.Timeout);
            notification.Clock = clock;
            scale.ApplyAnimationClock(ScaleTransform.ScaleXProperty, clock);
            container.MouseEnter += (_, _) => clock.Controller?.Pause();
            container.MouseLeave += (_, _) => clock.Controller?.Resume();
        }

        if (IsTop) _panel.Children.Insert(0, container);
        else _panel.Children.Add(container);
        AnimateIn(container);
    }

    private static bool IsTimed(Notification notification) => notification.Duration > TimeSpan.Zero && notification.Duration != Timeout.InfiniteTimeSpan;

    private static void AnimateIn(FrameworkElement element)
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(200));
        var translate = new TranslateTransform();
        element.RenderTransform = translate;
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, duration) { EasingFunction = new CubicEase() });
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = new CubicEase() });
    }

    private static void AnimateOut(FrameworkElement element, Action completed)
    {
        var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(180))) { EasingFunction = new CubicEase() };
        fade.Completed += (_, _) => completed();
        element.BeginAnimation(OpacityProperty, fade);
    }
}
