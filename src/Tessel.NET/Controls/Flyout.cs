using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Tessel.NET.Controls;

/// <summary>Where a <see cref="Flyout"/> or <see cref="TeachingTip"/> appears relative to its target.</summary>
public enum FlyoutPlacementMode
{
    Bottom,
    Top,
    Left,
    Right,
}

/// <summary>
/// A lightweight popup that shows arbitrary content next to an element.
/// Put it anywhere in the page, set <see cref="Target"/> (or call <see cref="ShowAt"/>) and toggle <see cref="IsOpen"/>.
/// <code>&lt;tessel:Flyout x:Name="Info" Target="{Binding ElementName=Button}"&gt;…content…&lt;/tessel:Flyout&gt;</code>
/// </summary>
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
public class Flyout : ContentControl
{
    private Popup? _popup;

    static Flyout()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Flyout), new FrameworkPropertyMetadata(typeof(Flyout)));
        FocusableProperty.OverrideMetadata(typeof(Flyout), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Flyout), new FrameworkPropertyMetadata(false));
    }

    #region Dependency properties

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(Flyout),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The element the flyout is anchored to.</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(UIElement), typeof(Flyout), new PropertyMetadata(null, (d, _) => ((Flyout)d).UpdatePopup()));

    public UIElement? Target
    {
        get => (UIElement?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(FlyoutPlacementMode), typeof(Flyout),
        new PropertyMetadata(FlyoutPlacementMode.Bottom, (d, _) => ((Flyout)d).UpdatePopup()));

    public FlyoutPlacementMode Placement
    {
        get => (FlyoutPlacementMode)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>
    /// When false (default) clicking outside the flyout closes it; when true it stays open until
    /// <see cref="Hide"/> is called or <see cref="IsOpen"/> is set to false.
    /// </summary>
    public static readonly DependencyProperty StaysOpenProperty = DependencyProperty.Register(
        nameof(StaysOpen), typeof(bool), typeof(Flyout), new PropertyMetadata(false, (d, _) => ((Flyout)d).UpdatePopup()));

    public bool StaysOpen
    {
        get => (bool)GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    #endregion

    public event EventHandler? Opened;
    public event EventHandler? Closed;

    public override void OnApplyTemplate()
    {
        if (_popup != null)
        {
            _popup.Opened -= OnPopupOpened;
            _popup.Closed -= OnPopupClosed;
        }

        base.OnApplyTemplate();

        _popup = GetTemplateChild("PART_Popup") as Popup;
        if (_popup != null)
        {
            _popup.Opened += OnPopupOpened;
            _popup.Closed += OnPopupClosed;
        }
        UpdatePopup();
    }

    /// <summary>Opens the flyout next to <paramref name="target"/>.</summary>
    public void ShowAt(UIElement target)
    {
        Target = target;
        IsOpen = true;
    }

    public void Hide() => IsOpen = false;

    internal void UpdatePopup()
    {
        if (_popup == null) return;
        _popup.PlacementTarget = Target ?? this;
        _popup.StaysOpen = StaysOpen;
        _popup.Placement = Placement switch
        {
            FlyoutPlacementMode.Top => PlacementMode.Top,
            FlyoutPlacementMode.Left => PlacementMode.Left,
            FlyoutPlacementMode.Right => PlacementMode.Right,
            _ => PlacementMode.Bottom,
        };
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var flyout = (Flyout)d;
        if ((bool)e.NewValue) flyout.UpdatePopup();
    }

    private void OnPopupOpened(object? sender, EventArgs e) => Opened?.Invoke(this, EventArgs.Empty);

    private void OnPopupClosed(object? sender, EventArgs e) => Closed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// An anchored, dismissible tip with a title, a message, optional content and up to two buttons.
/// Use it to explain a feature or guide the user to something in the UI.
/// </summary>
public class TeachingTip : Flyout
{
    private ButtonBase? _actionButton;
    private ButtonBase? _closeButton;
    private ButtonBase? _dismissButton;

    static TeachingTip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TeachingTip), new FrameworkPropertyMetadata(typeof(TeachingTip)));
        StaysOpenProperty.OverrideMetadata(typeof(TeachingTip), new PropertyMetadata(true));
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(TeachingTip), new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(TeachingTip), new PropertyMetadata(string.Empty));

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Glyph shown before the title (use <c>{tessel:Glyph ...}</c>).</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(TeachingTip), new PropertyMetadata(string.Empty));

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty ActionButtonContentProperty = DependencyProperty.Register(
        nameof(ActionButtonContent), typeof(object), typeof(TeachingTip), new PropertyMetadata(null));

    /// <summary>Content of the accent button (empty hides it).</summary>
    public object? ActionButtonContent
    {
        get => GetValue(ActionButtonContentProperty);
        set => SetValue(ActionButtonContentProperty, value);
    }

    public static readonly DependencyProperty CloseButtonContentProperty = DependencyProperty.Register(
        nameof(CloseButtonContent), typeof(object), typeof(TeachingTip), new PropertyMetadata(null));

    /// <summary>Content of the secondary button (empty hides it).</summary>
    public object? CloseButtonContent
    {
        get => GetValue(CloseButtonContentProperty);
        set => SetValue(CloseButtonContentProperty, value);
    }

    /// <summary>Shows the "x" button in the corner (default true).</summary>
    public static readonly DependencyProperty IsClosableProperty = DependencyProperty.Register(
        nameof(IsClosable), typeof(bool), typeof(TeachingTip), new PropertyMetadata(true));

    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public event EventHandler? ActionButtonClick;
    public event EventHandler? CloseButtonClick;

    public override void OnApplyTemplate()
    {
        if (_actionButton != null) _actionButton.Click -= OnActionClick;
        if (_closeButton != null) _closeButton.Click -= OnCloseClick;
        if (_dismissButton != null) _dismissButton.Click -= OnCloseClick;

        base.OnApplyTemplate();

        _actionButton = GetTemplateChild("PART_ActionButton") as ButtonBase;
        _closeButton = GetTemplateChild("PART_CloseButton") as ButtonBase;
        _dismissButton = GetTemplateChild("PART_DismissButton") as ButtonBase;

        if (_actionButton != null) _actionButton.Click += OnActionClick;
        if (_closeButton != null) _closeButton.Click += OnCloseClick;
        if (_dismissButton != null) _dismissButton.Click += OnCloseClick;
    }

    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        ActionButtonClick?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseButtonClick?.Invoke(this, EventArgs.Empty);
        Hide();
    }
}
