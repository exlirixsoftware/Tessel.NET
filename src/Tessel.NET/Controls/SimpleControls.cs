using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Tessel.NET.Markup;

namespace Tessel.NET.Controls;

/// <summary>A surface that groups related content, with an optional header.</summary>
public class Card : HeaderedContentControl
{
    static Card()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(Card)));
    }

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(Card), new PropertyMetadata(new CornerRadius(8)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Adds a drop shadow.</summary>
    public static readonly DependencyProperty IsElevatedProperty = DependencyProperty.Register(
        nameof(IsElevated), typeof(bool), typeof(Card), new PropertyMetadata(false));

    public bool IsElevated
    {
        get => (bool)GetValue(IsElevatedProperty);
        set => SetValue(IsElevatedProperty, value);
    }
}

public enum BadgeKind
{
    Neutral,
    Accent,
    Success,
    Warning,
    Danger,
    Info,
}

/// <summary>A small status label ("New", "Beta", "3").</summary>
public class Badge : ContentControl
{
    static Badge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(typeof(Badge)));
    }

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(BadgeKind), typeof(Badge), new PropertyMetadata(BadgeKind.Neutral));

    public BadgeKind Kind
    {
        get => (BadgeKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Uses a solid background instead of the subtle tint.</summary>
    public static readonly DependencyProperty IsSolidProperty = DependencyProperty.Register(
        nameof(IsSolid), typeof(bool), typeof(Badge), new PropertyMetadata(false));

    public bool IsSolid
    {
        get => (bool)GetValue(IsSolidProperty);
        set => SetValue(IsSolidProperty, value);
    }
}

/// <summary>An on/off switch.</summary>
public class ToggleSwitch : ToggleButton
{
    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch), new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
    }

    public static readonly DependencyProperty OnContentProperty = DependencyProperty.Register(
        nameof(OnContent), typeof(object), typeof(ToggleSwitch), new PropertyMetadata(null));

    /// <summary>Content shown next to the switch when on (falls back to <see cref="ContentControl.Content"/>).</summary>
    public object? OnContent
    {
        get => GetValue(OnContentProperty);
        set => SetValue(OnContentProperty, value);
    }

    public static readonly DependencyProperty OffContentProperty = DependencyProperty.Register(
        nameof(OffContent), typeof(object), typeof(ToggleSwitch), new PropertyMetadata(null));

    /// <summary>Content shown next to the switch when off (falls back to <see cref="ContentControl.Content"/>).</summary>
    public object? OffContent
    {
        get => GetValue(OffContentProperty);
        set => SetValue(OffContentProperty, value);
    }
}

/// <summary>Displays a glyph from the icon font.</summary>
public class FontIcon : Control
{
    static FontIcon()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FontIcon), new FrameworkPropertyMetadata(typeof(FontIcon)));
        FocusableProperty.OverrideMetadata(typeof(FontIcon), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(FontIcon), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(FontIcon), new PropertyMetadata(string.Empty));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol), typeof(Symbol), typeof(FontIcon),
        new PropertyMetadata(default(Symbol), (d, e) => ((FontIcon)d).Glyph = GlyphExtension.ToGlyph((Symbol)e.NewValue)));

    public Symbol Symbol
    {
        get => (Symbol)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }
}

/// <summary>A row used in settings pages: icon, header, description and a control on the right.</summary>
public class SettingsCard : ContentControl
{
    static SettingsCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingsCard), new FrameworkPropertyMetadata(typeof(SettingsCard)));
    }

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

    public object? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>A glyph string (use <c>{tessel:Glyph ...}</c>) or any element.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
