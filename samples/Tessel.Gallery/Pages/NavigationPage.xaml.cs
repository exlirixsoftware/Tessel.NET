using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Tessel.NET.Controls;

namespace Tessel.Gallery.Pages;

public partial class NavigationPage : UserControl
{
    private readonly ObservableCollection<string> _path = ["Home", "Documents", "Projects", "Tessel.NET", "README.md"];
    private int _tabCounter = 1;

    public NavigationPage()
    {
        InitializeComponent();
        Breadcrumbs.ItemsSource = _path;
    }

    private void OnCrumbClicked(object? sender, BreadcrumbBarItemClickedEventArgs e)
    {
        while (_path.Count > e.Index + 1) _path.RemoveAt(_path.Count - 1);
        CrumbStatus.Text = $"Navigated to “{e.Item}”.";
    }

    private void OnStepBack(object sender, RoutedEventArgs e) => Steps.CurrentStep--;

    private void OnStepNext(object sender, RoutedEventArgs e) => Steps.CurrentStep++;

    private void OnAddTab(object? sender, System.EventArgs e)
    {
        var tab = new TabViewItem
        {
            Header = $"Untitled {_tabCounter++}",
            Content = new TextBlock { Text = "A new tab.", Foreground = (System.Windows.Media.Brush)FindResource("Tessel.TextSecondaryBrush") },
        };
        Documents.Items.Add(tab);
        Documents.SelectedItem = tab;
    }

    private void OnTabCloseRequested(object? sender, TabCloseRequestedEventArgs e)
    {
        // By default the tab is removed; set e.Cancel = true to keep it (e.g. after asking to save).
    }
}
