using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Tessel.Gallery.Models;
using Tessel.NET.Controls;
using Tessel.NET.Theming;

namespace Tessel.Gallery.Pages;

public partial class ToolsPage : UserControl
{
    private static readonly string[] People = ["Ada Lovelace", "Alan Turing", "Grace Hopper", "Linus Torvalds", "Margaret Hamilton", "Dennis Ritchie", "Barbara Liskov", "Ken Thompson"];

    private readonly Random _random = new(7);
    private readonly ObservableCollection<TeamMember> _team = [];
    private readonly ShapeModel _shape = new();
    private string? _savedLayout;
    private int _taskCounter = 1;
    private Window? _window;

    public ToolsPage()
    {
        InitializeComponent();
        BuildDashboard();
        BuildPeople();
        BuildInputs();
        BuildInspector();
        BuildGantt();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #region Dashboard

    private void BuildDashboard()
    {
        LineSpark.Values = Enumerable.Range(0, 30).Select(i => 40 + 18 * Math.Sin(i / 4.0) + i * 1.2 + _random.Next(-5, 6)).ToArray();
        AreaSpark.Values = Enumerable.Range(0, 30).Select(i => 120 + 40 * Math.Cos(i / 5.0) + _random.Next(0, 25)).ToArray();
        BarSpark.Values = Enumerable.Range(0, 16).Select(_ => (double)_random.Next(-30, 80)).ToArray();

        var data = new Dictionary<DateTime, double>();
        for (var day = 0; day < 365; day++)
        {
            var date = DateTime.Today.AddDays(-day);
            var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (_random.NextDouble() < (weekend ? 0.3 : 0.75)) data[date] = _random.Next(1, 12) * (weekend ? 1 : 2);
        }
        Heatmap.Data = data;
    }

    private void OnRandomLoad(object sender, RoutedEventArgs e) => CpuGauge.Value = _random.Next(5, 99);

    private void OnHeatmapDay(object sender, RoutedEventArgs e)
    {
        var day = (HeatmapDayEventArgs)e;
        HeatmapInfo.Text = day.Value > 0 ? $"{day.Date:dddd, d MMMM yyyy}: {day.Value} contributions" : $"{day.Date:dddd, d MMMM yyyy}: no activity";
    }

    #endregion

    #region People and carousel

    private void BuildPeople()
    {
        var presences = new[] { AvatarPresence.Online, AvatarPresence.Away, AvatarPresence.Busy, AvatarPresence.Offline };
        for (var i = 0; i < 6; i++) _team.Add(new TeamMember(People[i], presences[i % presences.Length]));
        Team.ItemsSource = _team;
    }

    private void OnAddMember(object sender, RoutedEventArgs e)
    {
        if (_team.Count < People.Length) _team.Add(new TeamMember(People[_team.Count], AvatarPresence.Online));
    }

    private void OnRemoveMember(object sender, RoutedEventArgs e)
    {
        if (_team.Count > 0) _team.RemoveAt(_team.Count - 1);
    }

    private void OnAutoPlay(object sender, RoutedEventArgs e)
        => Slides.AutoPlayInterval = ((ToggleButton)sender).IsChecked == true ? TimeSpan.FromSeconds(2.5) : TimeSpan.Zero;

    #endregion

    #region Tags, mentions and palette

    private void BuildInputs()
    {
        Tags.Suggestions = new[] { "design", "development", "devops", "documentation", "research", "review", "release", "bug", "feature" };
        Tags.Tags.Add("feature");
        Tags.Tags.Add("design");
        Tags.TagAdded += (_, tag) => TagInfo.Text = $"Added “{tag}” ({Tags.Tags.Count} tags)";
        Tags.TagRemoved += (_, tag) => TagInfo.Text = $"Removed “{tag}” ({Tags.Tags.Count} tags)";

        Mentions.Candidates = People;
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(MentionBox.TextProperty, typeof(MentionBox))!.AddValueChanged(Mentions, (_, _) =>
        {
            var mentioned = Mentions.GetMentions();
            MentionInfo.Text = "Type @ and a name. Mentioned: " + (mentioned.Count == 0 ? "—" : string.Join(", ", mentioned));
        });
        Mentions.Text = "Thanks @Ada Lovelace for the review! ";
    }

    private List<PaletteCommand> Commands() =>
    [
        new("Toggle theme", () => ThemeManager.SetTheme(ThemeManager.ActualTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark)) { Category = "View", Glyph = "", Shortcut = "Ctrl+T", Keywords = "dark light appearance" },
        new("Show a notification", () => NotificationHost.Attach(_window)?.Show("Hello from the palette", "A command ran.", InfoBarSeverity.Success, actionText: "Undo", action: () => { })) { Category = "Demo", Glyph = "" },
        new("Random CPU load", () => CpuGauge.Value = _random.Next(5, 99)) { Category = "Demo", Glyph = "", Description = "Moves the gauge" },
        new("Add a Gantt task", () => OnGanttAdd(this, new RoutedEventArgs())) { Category = "Project", Glyph = "" },
        new("Fit the Gantt chart", () => Gantt.FitAll()) { Category = "Project", Glyph = "", Keywords = "zoom" },
        new("Reset the docking layout", () => { Dock.RestoreLayout(_savedLayout ?? string.Empty); OnShowAllPanes(this, new RoutedEventArgs()); }) { Category = "Layout", Glyph = "" },
        new("Delete everything", () => { }) { Category = "Danger", IsEnabled = false, Description = "Disabled commands are shown but cannot run" },
    ];

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window != null) CommandPalette.Attach(_window, Commands());
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window != null) CommandPalette.Detach(_window);
    }

    private void OnOpenPalette(object sender, RoutedEventArgs e) => CommandPalette.Show(Window.GetWindow(this), Commands());

    #endregion

    #region Inspector

    private void BuildInspector()
    {
        Inspector.SelectedObject = _shape;
        DockInspector.SelectedObject = _shape;
        Inspector.PropertyValueChanged += (_, e) => InspectorInfo.Text = $"{e.Entry.DisplayName} = {e.NewValue}";
        _shape.PropertyChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        PreviewText.Text = _shape.Label;
        Preview.Width = _shape.Width;
        Preview.Height = _shape.Height;
        Preview.Opacity = _shape.Opacity;
        Preview.Visibility = _shape.IsVisible ? Visibility.Visible : Visibility.Hidden;
        Preview.Background = new SolidColorBrush(_shape.Fill);
        Preview.CornerRadius = new CornerRadius(_shape.Kind switch
        {
            ShapeKind.Rectangle => 0,
            ShapeKind.Pill => _shape.Height / 2,
            _ => _shape.CornerRadius,
        });
    }

    #endregion

    #region Gantt and docking

    private void BuildGantt()
    {
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        var research = new GanttTask("Research", monday.AddDays(-7), monday.AddDays(5)) { Progress = 1, Assignee = "Ada" };
        var design = new GanttTask("Design", monday.AddDays(5), monday.AddDays(19)) { Progress = 0.6, Assignee = "Grace" };
        var build = new GanttTask("Build", monday.AddDays(19), monday.AddDays(47)) { Progress = 0.25, Brush = (Brush)Application.Current.Resources["Tessel.SuccessBrush"], Assignee = "Alan" };
        var docs = new GanttTask("Documentation", monday.AddDays(26), monday.AddDays(45)) { Progress = 0.1, Assignee = "Linus" };
        var test = new GanttTask("Test", monday.AddDays(47), monday.AddDays(57)) { Brush = (Brush)Application.Current.Resources["Tessel.WarningBrush"] };
        var release = new GanttTask("Release", monday.AddDays(57), monday.AddDays(58)) { IsMilestone = true };
        design.DependsOn.Add(research);
        build.DependsOn.Add(design);
        docs.DependsOn.Add(design);
        test.DependsOn.Add(build);
        release.DependsOn.Add(test);
        foreach (var task in new[] { research, design, build, docs, test, release }) Gantt.Tasks.Add(task);
    }

    private void OnGanttAdd(object sender, RoutedEventArgs e)
    {
        var start = DateTime.Today.AddDays(_taskCounter * 3);
        var task = new GanttTask($"New task {_taskCounter++}", start, start.AddDays(8));
        Gantt.Tasks.Add(task);
        Gantt.SelectTask(task);
        Gantt.ScrollToTask(task);
    }

    private void OnGanttFit(object sender, RoutedEventArgs e) => Gantt.FitAll();

    private void OnGanttZoomIn(object sender, RoutedEventArgs e) => Gantt.ZoomIn();

    private void OnGanttZoomOut(object sender, RoutedEventArgs e) => Gantt.ZoomOut();

    private void OnGanttChanged(object? sender, GanttTaskChangedEventArgs e)
        => GanttInfo.Text = e.Change switch
        {
            GanttChange.ProgressChanged => $"“{e.Task.Name}” is {e.Task.Progress:P0} done",
            GanttChange.Moved => $"“{e.Task.Name}” moved to {e.Task.Start:d MMM} - {e.Task.End.AddDays(-1):d MMM}",
            _ => $"“{e.Task.Name}” now lasts {e.Task.Duration} days ({e.Task.Start:d MMM} - {e.Task.End.AddDays(-1):d MMM})",
        };

    private void OnGanttSelection(object? sender, EventArgs e)
    {
        if (Gantt.SelectedTask is { } task) GanttInfo.Text = $"Selected “{task.Name}”, {task.Duration} days, {task.Progress:P0} done";
    }

    private void OnSaveLayout(object sender, RoutedEventArgs e) => _savedLayout = Dock.SaveLayout();

    private void OnRestoreLayout(object sender, RoutedEventArgs e)
    {
        if (_savedLayout != null) Dock.RestoreLayout(_savedLayout);
    }

    private void OnShowAllPanes(object sender, RoutedEventArgs e)
    {
        foreach (var pane in Dock.Panes.ToList())
        {
            if (!pane.IsOpen || pane.IsFloating) Dock.Show(pane);
        }
    }

    #endregion
}
