using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Tessel.UI.Controls;

/// <summary>A round picture of a person, falling back to their initials.</summary>
[TemplatePart(Name = "PART_Image", Type = typeof(Ellipse))]
public class Avatar : Control
{
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x4F, 0x46, 0xE5), Color.FromRgb(0x08, 0x91, 0xB2), Color.FromRgb(0x05, 0x96, 0x69),
        Color.FromRgb(0xD9, 0x77, 0x06), Color.FromRgb(0xDB, 0x27, 0x77), Color.FromRgb(0x7C, 0x3A, 0xED),
        Color.FromRgb(0xDC, 0x26, 0x26), Color.FromRgb(0x25, 0x63, 0xEB),
    ];

    private Ellipse? _image;

    static Avatar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(typeof(Avatar)));
        FocusableProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(
        nameof(DisplayName), typeof(string), typeof(Avatar), new PropertyMetadata(null, OnDisplayNameChanged));

    public string? DisplayName
    {
        get => (string?)GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        nameof(ImageSource), typeof(ImageSource), typeof(Avatar), new PropertyMetadata(null, (d, _) => ((Avatar)d).UpdateImage()));

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    /// <summary>When true (default) the background color is derived from <see cref="DisplayName"/>.</summary>
    public static readonly DependencyProperty IsColorFromNameProperty = DependencyProperty.Register(
        nameof(IsColorFromName), typeof(bool), typeof(Avatar), new PropertyMetadata(true, (d, _) => ((Avatar)d).UpdateFill()));

    public bool IsColorFromName
    {
        get => (bool)GetValue(IsColorFromNameProperty);
        set => SetValue(IsColorFromNameProperty, value);
    }

    private static readonly DependencyPropertyKey FillPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Fill), typeof(Brush), typeof(Avatar), new PropertyMetadata(null));

    /// <summary>The brush actually painted: <see cref="Control.Background"/> if set, otherwise a color derived from the name.</summary>
    public static readonly DependencyProperty FillProperty = FillPropertyKey.DependencyProperty;

    public Brush? Fill => (Brush?)GetValue(FillProperty);

    private static readonly DependencyPropertyKey InitialsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Initials), typeof(string), typeof(Avatar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty InitialsProperty = InitialsPropertyKey.DependencyProperty;

    public string Initials => (string)GetValue(InitialsProperty);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _image = GetTemplateChild("PART_Image") as Ellipse;
        UpdateImage();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size > 0 && ReadLocalValue(FontSizeProperty) == DependencyProperty.UnsetValue)
        {
            SetCurrentValue(FontSizeProperty, Math.Max(8, size * 0.38));
        }
    }

    private void UpdateImage()
    {
        if (_image == null) return;
        if (ImageSource is { } source)
        {
            _image.Fill = new ImageBrush(source) { Stretch = Stretch.UniformToFill };
            _image.Visibility = Visibility.Visible;
        }
        else
        {
            _image.Fill = null;
            _image.Visibility = Visibility.Collapsed;
        }
    }

    private static void OnDisplayNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var avatar = (Avatar)d;
        var name = avatar.DisplayName?.Trim() ?? string.Empty;
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = parts.Length switch
        {
            0 => string.Empty,
            1 => parts[0][..1],
            _ => string.Concat(parts[0][..1], parts.Last()[..1]),
        };
        avatar.SetValue(InitialsPropertyKey, initials.ToUpperInvariant());
        avatar.UpdateFill();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == BackgroundProperty) UpdateFill();
    }

    private void UpdateFill()
    {
        var name = DisplayName?.Trim() ?? string.Empty;
        if (Background != null || !IsColorFromName || name.Length == 0)
        {
            SetValue(FillPropertyKey, Background);
            return;
        }

        var hash = 0;
        foreach (var ch in name) hash = unchecked(hash * 31 + ch);
        var brush = new SolidColorBrush(Palette[(hash & int.MaxValue) % Palette.Length]);
        brush.Freeze();
        SetValue(FillPropertyKey, brush);
    }
}
