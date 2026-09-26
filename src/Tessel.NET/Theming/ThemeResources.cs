using System;
using System.Windows;
using System.Windows.Media;

namespace Tessel.NET.Theming;

/// <summary>The color theme of an application.</summary>
public enum AppTheme
{
    /// <summary>Follows the Windows "app mode" setting.</summary>
    System,
    Light,
    Dark,
}

/// <summary>
/// The Tessel resource dictionary. Merge it into <c>App.xaml</c>:
/// <code>
/// &lt;tessel:ThemeResources Theme="System" Accent="#0078D4" /&gt;
/// </code>
/// It contains the color palette, typography and the styles of all standard WPF controls.
/// </summary>
public class ThemeResources : ResourceDictionary
{
    private const string AssemblyPrefix = "/Tessel.NET;component/Themes/";

    private static readonly string[] AccentKeys =
    [
        "Tessel.AccentColor", "Tessel.AccentBrush", "Tessel.AccentHoverBrush",
        "Tessel.AccentPressedBrush", "Tessel.AccentSubtleBrush", "Tessel.OnAccentBrush",
    ];

    private AppTheme _theme = AppTheme.System;
    private Color? _accent;

    public ThemeResources()
    {
        MergedDictionaries.Add(LoadPalette(AppTheme.Light));
        MergedDictionaries.Add(Load("Base.xaml"));
        MergedDictionaries.Add(Load("Controls.xaml"));
        ActualTheme = AppTheme.Light;
        ThemeManager.Register(this);
        ApplyTheme();
    }

    /// <summary>The requested theme. <see cref="AppTheme.System"/> follows Windows.</summary>
    public AppTheme Theme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value;
            ApplyTheme();
        }
    }

    /// <summary>The theme currently applied (never <see cref="AppTheme.System"/>).</summary>
    public AppTheme ActualTheme { get; private set; }

    /// <summary>A custom accent color, or null for the built-in accent.</summary>
    public Color? Accent
    {
        get => _accent;
        set
        {
            _accent = value;
            ApplyAccent();
            ThemeManager.RaiseThemeChanged();
        }
    }

    internal void ApplyTheme()
    {
        var actual = _theme == AppTheme.System ? ThemeManager.GetSystemTheme() : _theme;
        if (actual != ActualTheme)
        {
            ActualTheme = actual;
            MergedDictionaries[0] = LoadPalette(actual);
        }
        ApplyAccent();
        ThemeManager.RaiseThemeChanged();
    }

    private void ApplyAccent()
    {
        foreach (var key in AccentKeys) Remove(key);
        if (_accent is not { } accent) return;

        var isDark = ActualTheme == AppTheme.Dark;
        var surface = isDark ? Color.FromRgb(0x18, 0x1B, 0x21) : Colors.White;

        var hover = isDark ? Mix(accent, Colors.White, 0.14) : Mix(accent, Colors.Black, 0.12);
        var pressed = isDark ? Mix(accent, Colors.Black, 0.15) : Mix(accent, Colors.Black, 0.24);
        var subtle = Mix(accent, surface, isDark ? 0.78 : 0.88);
        var onAccent = RelativeLuminance(accent) > 0.5 ? Color.FromRgb(0x11, 0x14, 0x18) : Colors.White;

        this["Tessel.AccentColor"] = accent;
        this["Tessel.AccentBrush"] = Frozen(accent);
        this["Tessel.AccentHoverBrush"] = Frozen(hover);
        this["Tessel.AccentPressedBrush"] = Frozen(pressed);
        this["Tessel.AccentSubtleBrush"] = Frozen(subtle);
        this["Tessel.OnAccentBrush"] = Frozen(onAccent);
    }

    private static ResourceDictionary LoadPalette(AppTheme theme)
        => Load(theme == AppTheme.Dark ? "Dark.xaml" : "Light.xaml");

    private static ResourceDictionary Load(string file)
        => new() { Source = new Uri(AssemblyPrefix + file, UriKind.Relative) };

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    internal static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
        (byte)Math.Round(from.R + (to.R - from.R) * amount),
        (byte)Math.Round(from.G + (to.G - from.G) * amount),
        (byte)Math.Round(from.B + (to.B - from.B) * amount));

    internal static double RelativeLuminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }
}
