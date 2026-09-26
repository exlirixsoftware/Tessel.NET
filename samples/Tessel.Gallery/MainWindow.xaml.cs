using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tessel.UI.Controls;
using Tessel.UI.Markup;
using Tessel.UI.Theming;

namespace Tessel.Gallery;

public partial class MainWindow : TesselWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Navigation.SelectedItem = Navigation.MenuItems[0];

        ThemeManager.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
        UpdateThemeGlyph();
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
        => ThemeManager.SetTheme(ThemeManager.ActualTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateThemeGlyph();

    private void UpdateThemeGlyph()
        => ThemeGlyph.Text = GlyphExtension.ToGlyph(ThemeManager.ActualTheme == AppTheme.Dark ? Symbol.QuietHours : Symbol.Brightness);

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { Text: { Length: > 0 } query }) return;

        var match = Navigation.MenuItems.Concat(Navigation.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Content?.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);

        if (match != null) Navigation.SelectedItem = match;
    }
}
