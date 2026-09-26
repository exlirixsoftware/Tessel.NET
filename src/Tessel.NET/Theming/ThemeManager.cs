using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Tessel.NET.Theming;

/// <summary>Switches theme and accent color at runtime.</summary>
public static class ThemeManager
{
    private static ThemeResources? _resources;
    private static bool _listeningToSystem;

    /// <summary>Raised after the theme or the accent color changes.</summary>
    public static event EventHandler? ThemeChanged;

    /// <summary>The requested theme (may be <see cref="AppTheme.System"/>).</summary>
    public static AppTheme Theme => _resources?.Theme ?? AppTheme.System;

    /// <summary>The theme currently applied: <see cref="AppTheme.Light"/> or <see cref="AppTheme.Dark"/>.</summary>
    public static AppTheme ActualTheme => _resources?.ActualTheme ?? AppTheme.Light;

    /// <summary>The custom accent color, or null when the built-in accent is used.</summary>
    public static Color? Accent => _resources?.Accent;

    public static void SetTheme(AppTheme theme) => Resources.Theme = theme;

    /// <summary>Sets a custom accent color. Pass null to restore the built-in accent.</summary>
    public static void SetAccent(Color? accent) => Resources.Accent = accent;

    /// <summary>Uses the Windows accent color, if one is available.</summary>
    public static void UseSystemAccent() => Resources.Accent = GetSystemAccentColor();

    /// <summary>Reads the Windows "app mode" setting.</summary>
    public static AppTheme GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch
        {
            // Registry not accessible: fall back to light.
        }
        return AppTheme.Light;
    }

    /// <summary>Reads the Windows accent color, or null when unavailable.</summary>
    public static Color? GetSystemAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                var value = unchecked((uint)abgr);
                return Color.FromRgb((byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF));
            }
        }
        catch
        {
            // Not available.
        }
        return null;
    }

    internal static void Register(ThemeResources resources)
    {
        _resources = resources;
        if (!_listeningToSystem)
        {
            _listeningToSystem = true;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
    }

    internal static void RaiseThemeChanged() => ThemeChanged?.Invoke(null, EventArgs.Empty);

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General || _resources is not { Theme: AppTheme.System } resources) return;
        Application.Current?.Dispatcher.BeginInvoke(resources.ApplyTheme);
    }

    private static ThemeResources Resources => _resources ?? throw new InvalidOperationException(
        "Tessel theme resources were not found. Add <tessel:ThemeResources /> to Application.Resources.MergedDictionaries.");
}
