using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tessel.UI.Controls;
using Tessel.UI.Theming;

namespace Tessel.Gallery.Pages;

public partial class SettingsPage : UserControl
{
    private bool _initializing = true;

    public SettingsPage()
    {
        InitializeComponent();
        _initializing = false;

        // The page is cached by the NavigationView, so subscribe only while it is shown.
        Loaded += (_, _) =>
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
            OnThemeChanged(null, EventArgs.Empty);
        };
        Unloaded += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // Keep the radios in sync when the theme is toggled from the title bar.
        _initializing = true;
        (ThemeManager.Theme switch { AppTheme.Light => LightRadio, AppTheme.Dark => DarkRadio, _ => SystemRadio }).IsChecked = true;
        _initializing = false;
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        ThemeManager.SetTheme(Enum.Parse<AppTheme>((string)((RadioButton)sender).Tag));
    }

    private void OnAccentChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        var tag = (string)((RadioButton)sender).Tag;
        ThemeManager.SetAccent(string.IsNullOrEmpty(tag) ? null : (Color)ColorConverter.ConvertFromString(tag));
    }

    private void OnUseSystemAccent(object sender, RoutedEventArgs e)
    {
        var accent = ThemeManager.GetSystemAccentColor();
        if (accent == null)
        {
            Snackbar.Show("No Windows accent color found", severity: InfoBarSeverity.Warning, owner: Window.GetWindow(this));
            return;
        }

        foreach (var swatch in AccentPanel.Children.OfType<RadioButton>()) swatch.IsChecked = false;
        ThemeManager.SetAccent(accent);
    }
}
