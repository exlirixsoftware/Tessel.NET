using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Tessel.NET.Controls;

/// <summary>
/// Picks a color from a saturation/brightness field, a hue strip, an optional alpha strip or a hex code.
/// <code>&lt;tessel:ColorPicker Color="{Binding Accent}" IsAlphaEnabled="True" /&gt;</code>
/// </summary>
[TemplatePart(Name = "PART_Spectrum", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_SpectrumBase", Type = typeof(Shape))]
[TemplatePart(Name = "PART_SpectrumThumb", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_HueStrip", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_HueThumb", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_AlphaStrip", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_AlphaFill", Type = typeof(Shape))]
[TemplatePart(Name = "PART_AlphaThumb", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_HexBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Preview", Type = typeof(Border))]
public class ColorPicker : Control
{
    private FrameworkElement? _spectrum;
    private Shape? _spectrumBase;
    private FrameworkElement? _spectrumThumb;
    private FrameworkElement? _hueStrip;
    private FrameworkElement? _hueThumb;
    private FrameworkElement? _alphaStrip;
    private Shape? _alphaFill;
    private FrameworkElement? _alphaThumb;
    private TextBox? _hexBox;
    private Border? _preview;

    // The color is kept as HSV so the hue survives grays and black.
    private double _hue;
    private double _saturation;
    private double _value = 1;
    private double _alpha = 1;
    private bool _updating;

    static ColorPicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(typeof(ColorPicker)));
        FocusableProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(false));
    }

    #region Dependency properties

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(ColorPicker),
        new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Shows the alpha strip and edits #AARRGGBB values.</summary>
    public static readonly DependencyProperty IsAlphaEnabledProperty = DependencyProperty.Register(
        nameof(IsAlphaEnabled), typeof(bool), typeof(ColorPicker), new PropertyMetadata(false, (d, _) => ((ColorPicker)d).OnAlphaModeChanged()));

    public bool IsAlphaEnabled
    {
        get => (bool)GetValue(IsAlphaEnabledProperty);
        set => SetValue(IsAlphaEnabledProperty, value);
    }

    public static readonly DependencyProperty IsHexInputVisibleProperty = DependencyProperty.Register(
        nameof(IsHexInputVisible), typeof(bool), typeof(ColorPicker), new PropertyMetadata(true));

    public bool IsHexInputVisible
    {
        get => (bool)GetValue(IsHexInputVisibleProperty);
        set => SetValue(IsHexInputVisibleProperty, value);
    }

    public static readonly RoutedEvent ColorChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ColorChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<Color>), typeof(ColorPicker));

    public event RoutedPropertyChangedEventHandler<Color> ColorChanged
    {
        add => AddHandler(ColorChangedEvent, value);
        remove => RemoveHandler(ColorChangedEvent, value);
    }

    #endregion

    public override void OnApplyTemplate()
    {
        Detach();
        base.OnApplyTemplate();

        _spectrum = GetTemplateChild("PART_Spectrum") as FrameworkElement;
        _spectrumBase = GetTemplateChild("PART_SpectrumBase") as Shape;
        _spectrumThumb = GetTemplateChild("PART_SpectrumThumb") as FrameworkElement;
        _hueStrip = GetTemplateChild("PART_HueStrip") as FrameworkElement;
        _hueThumb = GetTemplateChild("PART_HueThumb") as FrameworkElement;
        _alphaStrip = GetTemplateChild("PART_AlphaStrip") as FrameworkElement;
        _alphaFill = GetTemplateChild("PART_AlphaFill") as Shape;
        _alphaThumb = GetTemplateChild("PART_AlphaThumb") as FrameworkElement;
        _hexBox = GetTemplateChild("PART_HexBox") as TextBox;
        _preview = GetTemplateChild("PART_Preview") as Border;

        Attach(_spectrum, OnSpectrumInput);
        Attach(_hueStrip, OnHueInput);
        Attach(_alphaStrip, OnAlphaInput);
        if (_hexBox != null)
        {
            _hexBox.LostKeyboardFocus += OnHexCommit;
            _hexBox.KeyDown += OnHexKeyDown;
        }

        SyncFromColor();
        UpdateVisuals();
    }

    private void Detach()
    {
        foreach (var element in new[] { _spectrum, _hueStrip, _alphaStrip })
        {
            if (element == null) continue;
            element.MouseLeftButtonDown -= OnPointerDown;
            element.MouseMove -= OnPointerMove;
            element.MouseLeftButtonUp -= OnPointerUp;
            element.SizeChanged -= OnHostSizeChanged;
        }
        if (_hexBox != null)
        {
            _hexBox.LostKeyboardFocus -= OnHexCommit;
            _hexBox.KeyDown -= OnHexKeyDown;
        }
    }

    private void Attach(FrameworkElement? element, Action<Point, FrameworkElement> handler)
    {
        if (element == null) return;
        element.Tag = handler;
        element.MouseLeftButtonDown += OnPointerDown;
        element.MouseMove += OnPointerMove;
        element.MouseLeftButtonUp += OnPointerUp;
        element.SizeChanged += OnHostSizeChanged;
    }

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e) => UpdateThumbs();

    private void OnPointerDown(object sender, MouseButtonEventArgs e)
    {
        var element = (FrameworkElement)sender;
        element.CaptureMouse();
        Dispatch(element, e);
        e.Handled = true;
    }

    private void OnPointerMove(object sender, MouseEventArgs e)
    {
        var element = (FrameworkElement)sender;
        if (element.IsMouseCaptured) Dispatch(element, e);
    }

    private void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        var element = (FrameworkElement)sender;
        if (element.IsMouseCaptured) element.ReleaseMouseCapture();
        e.Handled = true;
    }

    private static void Dispatch(FrameworkElement element, MouseEventArgs e)
    {
        if (element.Tag is Action<Point, FrameworkElement> handler) handler(e.GetPosition(element), element);
    }

    private void OnSpectrumInput(Point point, FrameworkElement host)
    {
        _saturation = Clamp01(point.X / Math.Max(1, host.ActualWidth));
        _value = 1 - Clamp01(point.Y / Math.Max(1, host.ActualHeight));
        Commit();
    }

    private void OnHueInput(Point point, FrameworkElement host)
    {
        _hue = Clamp01(point.X / Math.Max(1, host.ActualWidth)) * 360;
        Commit();
    }

    private void OnAlphaInput(Point point, FrameworkElement host)
    {
        _alpha = Clamp01(point.X / Math.Max(1, host.ActualWidth));
        Commit();
    }

    private void OnHexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyHex();
            _hexBox?.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            UpdateHexText();
            e.Handled = true;
        }
    }

    private void OnHexCommit(object sender, KeyboardFocusChangedEventArgs e) => ApplyHex();

    private void ApplyHex()
    {
        if (_hexBox == null) return;
        var text = _hexBox.Text.Trim();
        if (text.Length > 0 && !text.StartsWith('#')) text = "#" + text;
        try
        {
            if (ColorConverter.ConvertFromString(text) is Color parsed)
            {
                if (!IsAlphaEnabled) parsed.A = 255;
                SetCurrentValue(ColorProperty, parsed);
            }
        }
        catch (FormatException)
        {
            // Invalid input: fall through and restore the current value.
        }
        UpdateHexText();
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (ColorPicker)d;
        if (!picker._updating) picker.SyncFromColor();
        picker.UpdateVisuals();
        picker.RaiseEvent(new RoutedPropertyChangedEventArgs<Color>((Color)e.OldValue, (Color)e.NewValue, ColorChangedEvent));
    }

    private void OnAlphaModeChanged()
    {
        if (!IsAlphaEnabled && _alpha < 1)
        {
            _alpha = 1;
            Commit();
        }
        UpdateVisuals();
    }

    /// <summary>Rebuilds the HSV state from <see cref="Color"/> (keeping the hue for grays).</summary>
    private void SyncFromColor()
    {
        var color = Color;
        ToHsv(color, out var h, out var s, out var v);
        if (s > 0.0001 && v > 0.0001) _hue = h;
        _saturation = s;
        _value = v;
        _alpha = color.A / 255d;
    }

    private void Commit()
    {
        _updating = true;
        try
        {
            SetCurrentValue(ColorProperty, FromHsv(_hue, _saturation, _value, IsAlphaEnabled ? _alpha : 1));
        }
        finally
        {
            _updating = false;
        }
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var color = Color;
        var hueColor = FromHsv(_hue, 1, 1, 1);

        if (_spectrumBase != null) _spectrumBase.Fill = new SolidColorBrush(hueColor);
        if (_alphaFill != null)
        {
            var opaque = Color.FromRgb(color.R, color.G, color.B);
            _alphaFill.Fill = new LinearGradientBrush(Color.FromArgb(0, opaque.R, opaque.G, opaque.B), opaque, 0);
        }
        if (_preview != null) _preview.Background = new SolidColorBrush(color);
        if (_alphaStrip != null) _alphaStrip.Visibility = IsAlphaEnabled ? Visibility.Visible : Visibility.Collapsed;

        UpdateHexText();
        UpdateThumbs();
    }

    private void UpdateHexText()
    {
        if (_hexBox == null || _hexBox.IsKeyboardFocused) return;
        var c = Color;
        _hexBox.Text = IsAlphaEnabled ? $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private void UpdateThumbs()
    {
        Place(_spectrumThumb, _spectrum, _saturation, 1 - _value);
        Place(_hueThumb, _hueStrip, _hue / 360, 0.5);
        Place(_alphaThumb, _alphaStrip, _alpha, 0.5);
    }

    private static void Place(FrameworkElement? thumb, FrameworkElement? host, double fx, double fy)
    {
        if (thumb == null || host == null) return;
        Canvas.SetLeft(thumb, fx * host.ActualWidth - thumb.Width / 2);
        Canvas.SetTop(thumb, fy * host.ActualHeight - thumb.Height / 2);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    internal static void ToHsv(Color color, out double h, out double s, out double v)
    {
        double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        v = max;
        s = max <= 0 ? 0 : delta / max;
        if (delta <= 0) h = 0;
        else if (max == r) h = 60 * (((g - b) / delta % 6 + 6) % 6);
        else if (max == g) h = 60 * ((b - r) / delta + 2);
        else h = 60 * ((r - g) / delta + 4);
    }

    internal static Color FromHsv(double h, double s, double v, double alpha)
    {
        h = (h % 360 + 360) % 360;
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        double r, g, b;
        switch ((int)(h / 60))
        {
            case 0: (r, g, b) = (c, x, 0); break;
            case 1: (r, g, b) = (x, c, 0); break;
            case 2: (r, g, b) = (0, c, x); break;
            case 3: (r, g, b) = (0, x, c); break;
            case 4: (r, g, b) = (x, 0, c); break;
            default: (r, g, b) = (c, 0, x); break;
        }
        return Color.FromArgb(
            (byte)Math.Round(Clamp01(alpha) * 255),
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
