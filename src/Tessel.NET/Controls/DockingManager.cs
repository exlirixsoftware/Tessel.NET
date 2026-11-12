using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Tessel.NET.Controls;

/// <summary>The edge of a <see cref="DockingManager"/> a <see cref="DockPane"/> is docked to.</summary>
public enum DockSide
{
    Left,
    Right,
    Top,
    Bottom,
}

/// <summary>A tool window of a <see cref="DockingManager"/> (a project tree, an inspector, an output log): a title and some content.</summary>
public class DockPane : ContentControl
{
    static DockPane()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DockPane), new FrameworkPropertyMetadata(typeof(DockPane)));
        FocusableProperty.OverrideMetadata(typeof(DockPane), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(DockPane), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DockPane), new PropertyMetadata(string.Empty, OnLayoutPropertyChanged));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(DockPane), new PropertyMetadata(null, OnLayoutPropertyChanged));

    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(
        nameof(Side), typeof(DockSide), typeof(DockPane), new PropertyMetadata(DockSide.Left, OnLayoutPropertyChanged));

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(DockPane), new PropertyMetadata(true, OnLayoutPropertyChanged));

    public static readonly DependencyProperty CanFloatProperty = DependencyProperty.Register(
        nameof(CanFloat), typeof(bool), typeof(DockPane), new PropertyMetadata(true));

    public static readonly DependencyProperty CanCloseProperty = DependencyProperty.Register(
        nameof(CanClose), typeof(bool), typeof(DockPane), new PropertyMetadata(true));

    private static readonly DependencyPropertyKey IsFloatingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsFloating), typeof(bool), typeof(DockPane), new PropertyMetadata(false));

    public static readonly DependencyProperty IsFloatingProperty = IsFloatingPropertyKey.DependencyProperty;

    internal DockingManager? Manager { get; set; }

    /// <summary>The name on the tab.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>An icon-font character shown before the title.</summary>
    public string? Glyph
    {
        get => (string?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>The edge the pane is docked to.</summary>
    public DockSide Side
    {
        get => (DockSide)GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>False hides the pane (use <see cref="DockingManager.Show"/> to bring it back).</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool CanFloat
    {
        get => (bool)GetValue(CanFloatProperty);
        set => SetValue(CanFloatProperty, value);
    }

    public bool CanClose
    {
        get => (bool)GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    /// <summary>True while the pane is in a window of its own.</summary>
    public bool IsFloating => (bool)GetValue(IsFloatingProperty);

    internal void SetFloating(bool value) => SetValue(IsFloatingPropertyKey, value);

    /// <summary>Identifies the pane in a saved layout (the title when not set).</summary>
    public string? PaneId { get; set; }

    internal string Id => string.IsNullOrEmpty(PaneId) ? Title : PaneId;

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((DockPane)d).Manager?.OnPaneChanged();
}

public sealed class DockPaneEventArgs(DockPane pane) : EventArgs
{
    public DockPane Pane { get; } = pane;
}

/// <summary>
/// The shell of an editor: tool panes docked to the four edges around a central <see cref="Document"/> area. Several panes on one edge are
/// tabs; the splitters resize the edges. The menu of a pane's tab moves it to another edge, floats it into a window of its own, or hides it,
/// and <see cref="SaveLayout"/> / <see cref="RestoreLayout"/> remember how the user arranged them.
/// <code>&lt;tessel:DockingManager Document="{Binding Canvas}"&gt;&lt;tessel:DockPane Title="Explorer" Side="Left" /&gt;&lt;/tessel:DockingManager&gt;</code>
/// </summary>
[ContentProperty(nameof(Panes))]
[TemplatePart(Name = "PART_Root", Type = typeof(Grid))]
public class DockingManager : Control
{
    private const double SplitterSize = 5;

    private Grid? _root;
    private readonly ContentPresenter _documentPresenter = new();
    private readonly Dictionary<DockSide, DockPane> _selected = [];
    private readonly Dictionary<DockPane, Window> _floating = [];
    private readonly List<Border> _bodies = [];
    private bool _rebuilding;
    private bool _closingFloat;

    static DockingManager()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DockingManager), new FrameworkPropertyMetadata(typeof(DockingManager)));
        FocusableProperty.OverrideMetadata(typeof(DockingManager), new FrameworkPropertyMetadata(false));
    }

    public DockingManager()
    {
        Panes.CollectionChanged += OnPanesChanged;
        _documentPresenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding(nameof(Document)) { Source = this });
    }

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(object), typeof(DockingManager), new PropertyMetadata(null));

    public static readonly DependencyProperty LeftSizeProperty = DependencyProperty.Register(
        nameof(LeftSize), typeof(double), typeof(DockingManager), new PropertyMetadata(260d, (d, _) => ((DockingManager)d).Rebuild(), CoerceSize));

    public static readonly DependencyProperty RightSizeProperty = DependencyProperty.Register(
        nameof(RightSize), typeof(double), typeof(DockingManager), new PropertyMetadata(260d, (d, _) => ((DockingManager)d).Rebuild(), CoerceSize));

    public static readonly DependencyProperty TopSizeProperty = DependencyProperty.Register(
        nameof(TopSize), typeof(double), typeof(DockingManager), new PropertyMetadata(180d, (d, _) => ((DockingManager)d).Rebuild(), CoerceSize));

    public static readonly DependencyProperty BottomSizeProperty = DependencyProperty.Register(
        nameof(BottomSize), typeof(double), typeof(DockingManager), new PropertyMetadata(200d, (d, _) => ((DockingManager)d).Rebuild(), CoerceSize));

    private static object CoerceSize(DependencyObject d, object value) => Math.Max(80, (double)value);

    /// <summary>The main content in the middle (the editor, the canvas, the document tabs).</summary>
    public object? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>The width of the left edge (the height of the top and bottom edges). The splitters change them.</summary>
    public double LeftSize
    {
        get => (double)GetValue(LeftSizeProperty);
        set => SetValue(LeftSizeProperty, value);
    }

    public double RightSize
    {
        get => (double)GetValue(RightSizeProperty);
        set => SetValue(RightSizeProperty, value);
    }

    public double TopSize
    {
        get => (double)GetValue(TopSizeProperty);
        set => SetValue(TopSizeProperty, value);
    }

    public double BottomSize
    {
        get => (double)GetValue(BottomSizeProperty);
        set => SetValue(BottomSizeProperty, value);
    }

    /// <summary>All the panes; the order is the order of the tabs.</summary>
    public ObservableCollection<DockPane> Panes { get; } = [];

    /// <summary>Raised when the user (or code) hid a pane.</summary>
    public event EventHandler<DockPaneEventArgs>? PaneClosed;

    /// <summary>Raised when a pane was moved to another edge.</summary>
    public event EventHandler<DockPaneEventArgs>? PaneDocked;

    /// <summary>Raised when a pane was put into, or taken back from, a window of its own.</summary>
    public event EventHandler<DockPaneEventArgs>? PaneFloatingChanged;

    /// <summary>Raised after anything about the arrangement changed.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>The panes that are docked to an edge and open, in tab order.</summary>
    public IReadOnlyList<DockPane> GetPanes(DockSide side) => Panes.Where(p => p.IsOpen && !p.IsFloating && p.Side == side).ToList();

    /// <summary>The tab that is shown at an edge (null when the edge is empty).</summary>
    public DockPane? GetSelected(DockSide side)
    {
        var panes = GetPanes(side);
        if (panes.Count == 0) return null;
        return _selected.TryGetValue(side, out var selected) && panes.Contains(selected) ? selected : panes[0];
    }

    #region Commands

    /// <summary>Docks the pane to an edge (taking it back from its window when it floats) and shows it.</summary>
    public void Dock(DockPane pane, DockSide side)
    {
        EnsureHome(pane);
        pane.Side = side;
        pane.IsOpen = true;
        _selected[side] = pane;
        Rebuild();
        PaneDocked?.Invoke(this, new DockPaneEventArgs(pane));
    }

    /// <summary>Hides the pane. <see cref="Show"/> brings it back.</summary>
    public bool Hide(DockPane pane)
    {
        if (!Panes.Contains(pane) || !pane.IsOpen || !pane.CanClose) return false;
        EnsureHome(pane);
        pane.IsOpen = false;
        Rebuild();
        PaneClosed?.Invoke(this, new DockPaneEventArgs(pane));
        return true;
    }

    /// <summary>Shows a hidden pane at its edge and makes it the selected tab.</summary>
    public void Show(DockPane pane) => Activate(pane);

    /// <summary>Makes the pane the selected tab of its edge (showing it first when it is hidden).</summary>
    public void Activate(DockPane pane)
    {
        if (!Panes.Contains(pane)) return;
        EnsureHome(pane);
        pane.IsOpen = true;
        _selected[pane.Side] = pane;
        Rebuild();
    }

    /// <summary>Moves the pane into a window of its own.</summary>
    public bool Float(DockPane pane)
    {
        if (!Panes.Contains(pane) || !pane.CanFloat || pane.IsFloating) return false;
        pane.IsOpen = true;
        pane.SetFloating(true);
        Rebuild();

        var window = new Window
        {
            Title = pane.Title,
            Width = Math.Max(280, pane.ActualWidth > 0 ? pane.ActualWidth : 360),
            Height = Math.Max(240, pane.ActualHeight > 0 ? pane.ActualHeight : 440),
            Owner = Window.GetWindow(this),
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        window.Content = pane;
        _floating[pane] = window;
        window.Closing += (_, _) =>
        {
            if (!_closingFloat) Redock(pane);
        };
        window.Show();
        PaneFloatingChanged?.Invoke(this, new DockPaneEventArgs(pane));
        return true;
    }

    /// <summary>Takes a floating pane back to its edge.</summary>
    private void EnsureHome(DockPane pane)
    {
        if (pane.IsFloating && _floating.TryGetValue(pane, out var window))
        {
            _closingFloat = true;
            try
            {
                window.Close();
            }
            finally
            {
                _closingFloat = false;
            }
            Redock(pane);
        }
    }

    private void Redock(DockPane pane)
    {
        if (!_floating.Remove(pane, out var window)) return;
        window.Content = null;
        pane.SetFloating(false);
        Rebuild();
        PaneFloatingChanged?.Invoke(this, new DockPaneEventArgs(pane));
    }

    #endregion

    #region Template and layout

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Grid;
        Rebuild();
    }

    private void OnPanesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (DockPane pane in e.OldItems)
            {
                if (pane.IsFloating) EnsureHome(pane);
                pane.Manager = null;
            }
        }
        if (e.NewItems != null)
        {
            foreach (DockPane pane in e.NewItems) pane.Manager = this;
        }
        Rebuild();
    }

    internal void OnPaneChanged() => Rebuild();

    private void Rebuild()
    {
        if (_root == null || _rebuilding) return;
        _rebuilding = true;
        try
        {
            foreach (var body in _bodies) body.Child = null;
            _bodies.Clear();
            _root.Children.Clear();
            _root.RowDefinitions.Clear();
            _root.ColumnDefinitions.Clear();

            var top = Group(DockSide.Top);
            var bottom = Group(DockSide.Bottom);
            var left = Group(DockSide.Left);
            var right = Group(DockSide.Right);

            _root.RowDefinitions.Add(new RowDefinition { Height = top != null ? new GridLength(TopSize) : new GridLength(0), MinHeight = top != null ? 80 : 0 });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(top != null ? SplitterSize : 0) });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 80 });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(bottom != null ? SplitterSize : 0) });
            _root.RowDefinitions.Add(new RowDefinition { Height = bottom != null ? new GridLength(BottomSize) : new GridLength(0), MinHeight = bottom != null ? 80 : 0 });

            if (top != null)
            {
                Place(top, 0, 0);
                _root.Children.Add(Splitter(false, 1, 0, () => TopSize = _root.RowDefinitions[0].ActualHeight));
            }
            if (bottom != null)
            {
                Place(bottom, 4, 0);
                _root.Children.Add(Splitter(false, 3, 0, () => BottomSize = _root.RowDefinitions[4].ActualHeight));
            }

            var middle = new Grid();
            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = left != null ? new GridLength(LeftSize) : new GridLength(0), MinWidth = left != null ? 80 : 0 });
            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(left != null ? SplitterSize : 0) });
            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });
            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(right != null ? SplitterSize : 0) });
            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = right != null ? new GridLength(RightSize) : new GridLength(0), MinWidth = right != null ? 80 : 0 });

            if (left != null)
            {
                Grid.SetColumn(left, 0);
                middle.Children.Add(left);
                middle.Children.Add(Splitter(true, 0, 1, () => LeftSize = middle.ColumnDefinitions[0].ActualWidth));
            }
            if (right != null)
            {
                Grid.SetColumn(right, 4);
                middle.Children.Add(right);
                middle.Children.Add(Splitter(true, 0, 3, () => RightSize = middle.ColumnDefinitions[4].ActualWidth));
            }

            if (_documentPresenter.Parent is Panel old) old.Children.Remove(_documentPresenter);
            Grid.SetColumn(_documentPresenter, 2);
            middle.Children.Add(_documentPresenter);
            Grid.SetRow(middle, 2);
            _root.Children.Add(middle);
        }
        finally
        {
            _rebuilding = false;
        }
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Place(FrameworkElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        _root!.Children.Add(element);
    }

    private GridSplitter Splitter(bool vertical, int row, int column, Action completed)
    {
        var splitter = new GridSplitter
        {
            Background = Brushes.Transparent,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ShowsPreview = false,
            Focusable = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            Cursor = vertical ? Cursors.SizeWE : Cursors.SizeNS,
        };
        Grid.SetRow(splitter, row);
        Grid.SetColumn(splitter, column);
        if (!vertical) Grid.SetColumnSpan(splitter, 1);
        splitter.DragCompleted += (_, _) => completed();
        return splitter;
    }

    #endregion

    #region Groups

    private Border? Group(DockSide side)
    {
        var panes = GetPanes(side);
        if (panes.Count == 0) return null;
        var selected = GetSelected(side)!;
        _selected[side] = selected;

        var card = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), ClipToBounds = true };
        card.SetResourceReference(Border.BackgroundProperty, "Tessel.SurfaceBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "Tessel.BorderBrush");

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var strip = new DockPanel { LastChildFill = true, MinHeight = 34 };
        var stripBorder = new Border { Child = strip, BorderThickness = new Thickness(0, 0, 0, 1) };
        stripBorder.SetResourceReference(Border.BorderBrushProperty, "Tessel.BorderBrush");

        var close = HeaderButton("", "Hide");
        close.IsEnabled = selected.CanClose;
        close.Click += (_, _) => Hide(selected);
        DockPanel.SetDock(close, System.Windows.Controls.Dock.Right);
        strip.Children.Add(close);

        var more = HeaderButton("", "Move, float or hide");
        more.Click += (_, _) =>
        {
            var menu = BuildMenu(selected);
            menu.PlacementTarget = more;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        };
        DockPanel.SetDock(more, System.Windows.Controls.Dock.Right);
        strip.Children.Add(more);

        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var pane in panes) tabs.Children.Add(Tab(pane, pane == selected, side));
        strip.Children.Add(tabs);

        Grid.SetRow(stripBorder, 0);
        layout.Children.Add(stripBorder);

        var body = new Border { Child = selected };
        _bodies.Add(body);
        Grid.SetRow(body, 1);
        layout.Children.Add(body);

        card.Child = layout;
        return card;
    }

    private FrameworkElement Tab(DockPane pane, bool isSelected, DockSide side)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (!string.IsNullOrEmpty(pane.Glyph))
        {
            var icon = new TextBlock { Text = pane.Glyph, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
            icon.SetResourceReference(TextBlock.FontFamilyProperty, "Tessel.IconFontFamily");
            icon.SetResourceReference(TextBlock.ForegroundProperty, isSelected ? "Tessel.AccentBrush" : "Tessel.TextSecondaryBrush");
            row.Children.Add(icon);
        }
        var title = new TextBlock { Text = pane.Title, VerticalAlignment = VerticalAlignment.Center, FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal };
        title.SetResourceReference(TextBlock.ForegroundProperty, isSelected ? "Tessel.TextBrush" : "Tessel.TextSecondaryBrush");
        row.Children.Add(title);

        var tab = new Border
        {
            Child = row,
            Padding = new Thickness(12, 7, 12, 5),
            BorderThickness = new Thickness(0, 0, 0, 2),
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            Tag = pane,
        };
        tab.SetResourceReference(Border.BorderBrushProperty, isSelected ? "Tessel.AccentBrush" : "Tessel.SurfaceBrush");
        tab.MouseLeftButtonDown += (_, e) =>
        {
            Activate(pane);
            e.Handled = true;
        };
        tab.ContextMenu = BuildMenu(pane);
        return tab;
    }

    private static Button HeaderButton(string glyph, string tip)
    {
        var button = new Button
        {
            Content = glyph,
            ToolTip = tip,
            Width = 28,
            Height = 28,
            Margin = new Thickness(0, 2, 3, 2),
            Focusable = false,
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            FontSize = 12,
            Style = null,
        };
        button.SetResourceReference(Control.FontFamilyProperty, "Tessel.IconFontFamily");
        button.SetResourceReference(Control.ForegroundProperty, "Tessel.TextSecondaryBrush");
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        template.Triggers.Add(hover);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.35));
        template.Triggers.Add(disabled);
        button.Template = template;
        return button;
    }

    private ContextMenu BuildMenu(DockPane pane)
    {
        var menu = new ContextMenu();
        foreach (var side in Enum.GetValues<DockSide>())
        {
            if (side == pane.Side && !pane.IsFloating) continue;
            var item = new MenuItem { Header = "Move to " + side.ToString().ToLowerInvariant() };
            var target = side;
            item.Click += (_, _) => Dock(pane, target);
            menu.Items.Add(item);
        }
        if (pane.CanFloat && !pane.IsFloating)
        {
            var item = new MenuItem { Header = "Float" };
            item.Click += (_, _) => Float(pane);
            menu.Items.Add(item);
        }
        if (pane.CanClose)
        {
            menu.Items.Add(new Separator());
            var item = new MenuItem { Header = "Hide" };
            item.Click += (_, _) => Hide(pane);
            menu.Items.Add(item);
        }
        return menu;
    }

    /// <summary>The titles of the menu items of a pane (for tests and for building your own menu).</summary>
    internal IReadOnlyList<string> MenuFor(DockPane pane) => BuildMenu(pane).Items.OfType<MenuItem>().Select(m => m.Header?.ToString() ?? string.Empty).ToList();

    #endregion

    #region Layout storage

    /// <summary>The arrangement as text: the edge, the visibility and the selected tab of every pane, in tab order, and the sizes of the edges.</summary>
    public string SaveLayout()
    {
        var lines = new List<string>();
        foreach (var pane in Panes)
        {
            var selected = _selected.TryGetValue(pane.Side, out var s) && s == pane;
            lines.Add(string.Join('|', "pane", Escape(pane.Id), pane.Side, pane.IsOpen ? 1 : 0, selected ? 1 : 0));
        }
        lines.Add(string.Join('|', "size", LeftSize.ToString("R", CultureInfo.InvariantCulture), RightSize.ToString("R", CultureInfo.InvariantCulture),
            TopSize.ToString("R", CultureInfo.InvariantCulture), BottomSize.ToString("R", CultureInfo.InvariantCulture)));
        return string.Join('\n', lines);
    }

    /// <summary>Puts the panes back as <see cref="SaveLayout"/> described (panes that are not in the text keep their place); false when the text is no layout.</summary>
    public bool RestoreLayout(string? layout)
    {
        if (string.IsNullOrWhiteSpace(layout)) return false;
        var applied = false;
        var order = new List<DockPane>();
        _rebuilding = true;
        try
        {
            foreach (var pane in Panes.Where(p => p.IsFloating).ToList()) EnsureHome(pane);
            foreach (var line in layout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.TrimEnd('\r').Split('|');
                if (parts[0] == "pane" && parts.Length >= 5)
                {
                    var id = Unescape(parts[1]);
                    var pane = Panes.FirstOrDefault(p => p.Id == id);
                    if (pane == null || !Enum.TryParse<DockSide>(parts[2], out var side)) continue;
                    pane.Side = side;
                    pane.IsOpen = parts[3] == "1";
                    if (parts[4] == "1") _selected[side] = pane;
                    order.Add(pane);
                    applied = true;
                }
                else if (parts[0] == "size" && parts.Length >= 5)
                {
                    if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var l)) LeftSize = l;
                    if (double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var r)) RightSize = r;
                    if (double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) TopSize = t;
                    if (double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)) BottomSize = b;
                    applied = true;
                }
            }
            for (var i = 0; i < order.Count; i++)
            {
                var at = Panes.IndexOf(order[i]);
                if (at != i && i < Panes.Count) Panes.Move(at, i);
            }
        }
        finally
        {
            _rebuilding = false;
        }
        Rebuild();
        return applied;
    }

    private static string Escape(string text) => text.Replace("%", "%25").Replace("|", "%7C").Replace("\n", "%0A");

    private static string Unescape(string text) => text.Replace("%0A", "\n").Replace("%7C", "|").Replace("%25", "%");

    #endregion
}
