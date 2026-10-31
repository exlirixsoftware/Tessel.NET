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

    private void OnTimelineLayoutChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DemoTimeline == null || sender is not SegmentedControl { SelectedIndex: >= 0 } switcher) return;
        DemoTimeline.Orientation = switcher.SelectedIndex >= 2 ? Orientation.Horizontal : Orientation.Vertical;
        DemoTimeline.Layout = switcher.SelectedIndex is 1 or 3 ? TimelineLayout.Alternate : TimelineLayout.Standard;
    }

    private static readonly (string Title, string Text, BadgeKind Kind, string Glyph)[] NewEvents =
    [
        ("Release tagged", "v1.2.0 was tagged on main.", BadgeKind.Success, "\uE8EC"),
        ("Package published", "Tessel.NET 1.2.0 is available on NuGet.", BadgeKind.Accent, "\uE896"),
        ("Issue reported", "A user asked for a horizontal layout.", BadgeKind.Danger, "\uE7BA"),
        ("Docs updated", "The README now covers the timeline.", BadgeKind.Info, "\uE8A5"),
    ];

    private int _newEventIndex;

    private void OnAddActivity(object sender, RoutedEventArgs e)
    {
        var next = NewEvents[_newEventIndex++ % NewEvents.Length];
        Models.SampleData.Activity.Add(new Models.ActivityEvent
        {
            Title = next.Title,
            Description = next.Text,
            Kind = next.Kind,
            Glyph = next.Glyph,
            Time = System.DateTime.Now,
        });
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
