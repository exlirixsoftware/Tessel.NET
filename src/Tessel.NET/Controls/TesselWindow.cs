using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Tessel.NET.Theming;

namespace Tessel.NET.Controls;

/// <summary>
/// A window with a themed, custom-drawn title bar. Derive your windows from it:
/// <code>&lt;tessel:TesselWindow x:Class="App.MainWindow" ...&gt;</code>
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(FrameworkElement))]
public class TesselWindow : Window
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const int SM_CXPADDEDBORDER = 92;

    private readonly WindowChrome _chrome;

    static TesselWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TesselWindow), new FrameworkPropertyMetadata(typeof(TesselWindow)));
    }

    public TesselWindow()
    {
        _chrome = new WindowChrome
        {
            CaptionHeight = TitleBarHeight,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        };
        WindowChrome.SetWindowChrome(this, _chrome);

        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(this), CanAlways));
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this), CanAlways));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(this), CanAlways));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(this), CanAlways));
        CommandBindings.Add(new CommandBinding(SystemCommands.ShowSystemMenuCommand, OnShowSystemMenu, CanAlways));

        ThemeManager.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    #region Dependency properties

    /// <summary>Extra content placed in the title bar (search box, buttons...).</summary>
    public static readonly DependencyProperty TitleBarContentProperty = DependencyProperty.Register(
        nameof(TitleBarContent), typeof(object), typeof(TesselWindow), new PropertyMetadata(null));

    public object? TitleBarContent
    {
        get => GetValue(TitleBarContentProperty);
        set => SetValue(TitleBarContentProperty, value);
    }

    public static readonly DependencyProperty TitleBarHeightProperty = DependencyProperty.Register(
        nameof(TitleBarHeight), typeof(double), typeof(TesselWindow),
        new PropertyMetadata(36d, (d, e) => ((TesselWindow)d)._chrome.CaptionHeight = (double)e.NewValue));

    public double TitleBarHeight
    {
        get => (double)GetValue(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    /// <summary>Width and height of the window icon shown in the title bar (default 16).</summary>
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize), typeof(double), typeof(TesselWindow), new PropertyMetadata(16d));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly DependencyProperty ShowTitleProperty = DependencyProperty.Register(
        nameof(ShowTitle), typeof(bool), typeof(TesselWindow), new PropertyMetadata(true));

    public bool ShowTitle
    {
        get => (bool)GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    private static readonly DependencyPropertyKey ChromePaddingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ChromePadding), typeof(Thickness), typeof(TesselWindow), new PropertyMetadata(default(Thickness)));

    /// <summary>Padding that compensates for the off-screen frame when maximized.</summary>
    public static readonly DependencyProperty ChromePaddingProperty = ChromePaddingPropertyKey.DependencyProperty;

    public Thickness ChromePadding => (Thickness)GetValue(ChromePaddingProperty);

    #endregion

    /// <summary>The element that dialogs and snackbars are overlaid on.</summary>
    internal FrameworkElement? OverlayRoot { get; private set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        OverlayRoot = GetTemplateChild("PART_Root") as FrameworkElement;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyDwmAttributes();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateChromePadding();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateChromePadding();
    }

    private void UpdateChromePadding()
    {
        if (WindowState != WindowState.Maximized)
        {
            SetValue(ChromePaddingPropertyKey, default(Thickness));
            return;
        }

        var frame = SystemParameters.WindowResizeBorderThickness;
        var padded = GetSystemMetrics(SM_CXPADDEDBORDER) / VisualTreeHelper.GetDpi(this).DpiScaleX;
        SetValue(ChromePaddingPropertyKey, new Thickness(
            frame.Left + padded, frame.Top + padded, frame.Right + padded, frame.Bottom + padded));
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyDwmAttributes();

    private void ApplyDwmAttributes()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || Environment.OSVersion.Version.Build < 17763) return;

        try
        {
            var dark = ThemeManager.ActualTheme == AppTheme.Dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            if (Environment.OSVersion.Version.Build >= 22000)
            {
                var corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
            // DWM unavailable: cosmetic only.
        }
    }

    private void OnShowSystemMenu(object sender, ExecutedRoutedEventArgs e)
    {
        var point = PointToScreen(new Point(0, TitleBarHeight));
        var dpi = VisualTreeHelper.GetDpi(this);
        SystemCommands.ShowSystemMenu(this, new Point(point.X / dpi.DpiScaleX, point.Y / dpi.DpiScaleY));
    }

    private static void CanAlways(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
