using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Tessel.NET.Controls;
using Tessel.NET.Markup;

namespace Tessel.Gallery.Pages;

public partial class IconsPage : UserControl
{
    private static readonly Symbol[] AllSymbols = Enum.GetValues<Symbol>().OrderBy(s => s.ToString()).ToArray();

    public IconsPage()
    {
        InitializeComponent();
        IconList.ItemsSource = AllSymbols;
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        var query = Filter.Text.Trim();
        IconList.ItemsSource = query.Length == 0
            ? AllSymbols
            : AllSymbols.Where(s => s.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private void OnIconSelected(object sender, SelectionChangedEventArgs e)
    {
        if (IconList.SelectedItem is not Symbol symbol) return;
        var xaml = $"{{tessel:Glyph {symbol}}}";
        try
        {
            Clipboard.SetText(xaml);
            Snackbar.Show("Copied to clipboard", xaml, InfoBarSeverity.Success, TimeSpan.FromSeconds(2), Window.GetWindow(this));
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Clipboard is busy; ignore.
        }
    }
}
