using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Tessel.NET.Mvvm;

namespace Tessel.NET.Controls;

/// <summary>One bar of a <see cref="GanttChart"/>: a task with a start, an end and some progress, or a milestone.</summary>
public sealed class GanttTask : ObservableObject
{
    private string _name = string.Empty;
    private DateTime _start = DateTime.Today;
    private DateTime _end = DateTime.Today.AddDays(1);
    private double _progress;
    private Brush? _brush;
    private bool _isMilestone;
    private string? _assignee;
    private object? _tag;

    public GanttTask()
    {
    }

    public GanttTask(string name, DateTime start, DateTime end)
    {
        _name = name;
        _start = start.Date;
        _end = end.Date > start.Date ? end.Date : start.Date.AddDays(1);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    /// <summary>The first day of the task.</summary>
    public DateTime Start
    {
        get => _start;
        set
        {
            if (SetProperty(ref _start, value.Date)) OnPropertyChanged(nameof(Duration));
        }
    }

    /// <summary>The day after the last day of the task (a task of one day has Start = Monday and End = Tuesday).</summary>
    public DateTime End
    {
        get => _end;
        set
        {
            if (SetProperty(ref _end, value.Date)) OnPropertyChanged(nameof(Duration));
        }
    }

    /// <summary>The length in days.</summary>
    public int Duration => Math.Max(0, (_end - _start).Days);

    /// <summary>How much of the task is done, 0 to 1.</summary>
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, Math.Clamp(value, 0, 1));
    }

    /// <summary>The color of the bar (null = the color of the chart).</summary>
    public Brush? Brush
    {
        get => _brush;
        set => SetProperty(ref _brush, value);
    }

    /// <summary>Draws a diamond on the <see cref="Start"/> day instead of a bar.</summary>
    public bool IsMilestone
    {
        get => _isMilestone;
        set => SetProperty(ref _isMilestone, value);
    }

    public string? Assignee
    {
        get => _assignee;
        set => SetProperty(ref _assignee, value);
    }

    public object? Tag
    {
        get => _tag;
        set => SetProperty(ref _tag, value);
    }

    /// <summary>The tasks that must be finished before this one starts (drawn as arrows).</summary>
    public ObservableCollection<GanttTask> DependsOn { get; } = [];

    public override string ToString() => $"{Name} ({Start:d} - {End:d})";
}

/// <summary>What the user did to a task of a <see cref="GanttChart"/>.</summary>
public enum GanttChange
{
    Moved,
    Resized,
    ProgressChanged,
}

public sealed class GanttTaskChangedEventArgs(GanttTask task, GanttChange change, DateTime oldStart, DateTime oldEnd, double oldProgress) : EventArgs
{
    public GanttTask Task { get; } = task;

    public GanttChange Change { get; } = change;

    public DateTime OldStart { get; } = oldStart;

    public DateTime OldEnd { get; } = oldEnd;

    public double OldProgress { get; } = oldProgress;
}

/// <summary>
/// A project schedule: a list of tasks on the left and a bar for each on a calendar, with the progress of every task, milestones, arrows for
/// the tasks that depend on each other and a line for today. Drag a bar to move it, drag its ends to change the dates, drag the little
/// handle under it to set the progress. Ctrl + mouse wheel zooms.
/// </summary>
[TemplatePart(Name = "PART_HScroll", Type = typeof(ScrollBar))]
[TemplatePart(Name = "PART_VScroll", Type = typeof(ScrollBar))]
public class GanttChart : Control
{
    private const double HeaderHeight = 50;
    private const double ScrollSize = 16;
    private const double EdgeGrip = 6;
    private enum DragMode
    {
        None,
        Move,
        ResizeStart,
        ResizeEnd,
        Progress,
    }

    private ScrollBar? _hScroll;
    private ScrollBar? _vScroll;
    private double _hOffset;
    private double _vOffset;
    private DragMode _drag;
    private GanttTask? _dragTask;
    private Point _downPoint;
    private DateTime _origStart;
    private DateTime _origEnd;
    private double _origProgress;
    private GanttTask? _hover;
    private bool _changed;

    static GanttChart()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GanttChart), new FrameworkPropertyMetadata(typeof(GanttChart)));
        FocusableProperty.OverrideMetadata(typeof(GanttChart), new FrameworkPropertyMetadata(true));
    }

    public GanttChart()
    {
        Tasks.CollectionChanged += OnTasksChanged;
    }

    #region Dependency properties

    public static readonly DependencyProperty DayWidthProperty = DependencyProperty.Register(
        nameof(DayWidth), typeof(double), typeof(GanttChart),
        new FrameworkPropertyMetadata(28d, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GanttChart)d).UpdateScroll(), (_, v) => Math.Clamp((double)v, 3, 160)));

    public static readonly DependencyProperty RowHeightProperty = DependencyProperty.Register(
        nameof(RowHeight), typeof(double), typeof(GanttChart),
        new FrameworkPropertyMetadata(36d, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GanttChart)d).UpdateScroll(), (_, v) => Math.Clamp((double)v, 22, 80)));

    public static readonly DependencyProperty NameColumnWidthProperty = DependencyProperty.Register(
        nameof(NameColumnWidth), typeof(double), typeof(GanttChart),
        new FrameworkPropertyMetadata(220d, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GanttChart)d).UpdateScroll(), (_, v) => Math.Clamp((double)v, 0, 600)));

    public static readonly DependencyProperty ViewStartProperty = DependencyProperty.Register(
        nameof(ViewStart), typeof(DateTime?), typeof(GanttChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GanttChart)d).UpdateScroll()));

    public static readonly DependencyProperty ShowTodayProperty = DependencyProperty.Register(
        nameof(ShowToday), typeof(bool), typeof(GanttChart), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowDependenciesProperty = DependencyProperty.Register(
        nameof(ShowDependencies), typeof(bool), typeof(GanttChart), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable), typeof(bool), typeof(GanttChart), new PropertyMetadata(true));

    public static readonly DependencyProperty TodayProperty = DependencyProperty.Register(
        nameof(Today), typeof(DateTime?), typeof(GanttChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(GanttChart), new FrameworkPropertyMetadata(Brushes.RoyalBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(GanttChart), new FrameworkPropertyMetadata(Brushes.Gainsboro, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WeekendBrushProperty = DependencyProperty.Register(
        nameof(WeekendBrush), typeof(Brush), typeof(GanttChart), new FrameworkPropertyMetadata(Brushes.WhiteSmoke, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TodayBrushProperty = DependencyProperty.Register(
        nameof(TodayBrush), typeof(Brush), typeof(GanttChart), new FrameworkPropertyMetadata(Brushes.IndianRed, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyPropertyKey SelectedTaskPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SelectedTask), typeof(GanttTask), typeof(GanttChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedTaskProperty = SelectedTaskPropertyKey.DependencyProperty;

    /// <summary>How wide one day is, in pixels (3 to 160). Ctrl + mouse wheel changes it.</summary>
    public double DayWidth
    {
        get => (double)GetValue(DayWidthProperty);
        set => SetValue(DayWidthProperty, value);
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>The width of the column with the task names (0 hides it).</summary>
    public double NameColumnWidth
    {
        get => (double)GetValue(NameColumnWidthProperty);
        set => SetValue(NameColumnWidthProperty, value);
    }

    /// <summary>The first day of the calendar (null = a week before the first task).</summary>
    public DateTime? ViewStart
    {
        get => (DateTime?)GetValue(ViewStartProperty);
        set => SetValue(ViewStartProperty, value);
    }

    /// <summary>Draws a line on today.</summary>
    public bool ShowToday
    {
        get => (bool)GetValue(ShowTodayProperty);
        set => SetValue(ShowTodayProperty, value);
    }

    /// <summary>Draws arrows between tasks that depend on each other.</summary>
    public bool ShowDependencies
    {
        get => (bool)GetValue(ShowDependenciesProperty);
        set => SetValue(ShowDependenciesProperty, value);
    }

    /// <summary>False makes the chart read-only.</summary>
    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>The day that is drawn as today (null = the real today); useful for demos and tests.</summary>
    public DateTime? Today
    {
        get => (DateTime?)GetValue(TodayProperty);
        set => SetValue(TodayProperty, value);
    }

    /// <summary>The color of the bars that have no brush of their own.</summary>
    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public Brush GridBrush
    {
        get => (Brush)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public Brush WeekendBrush
    {
        get => (Brush)GetValue(WeekendBrushProperty);
        set => SetValue(WeekendBrushProperty, value);
    }

    public Brush TodayBrush
    {
        get => (Brush)GetValue(TodayBrushProperty);
        set => SetValue(TodayBrushProperty, value);
    }

    public GanttTask? SelectedTask
    {
        get => (GanttTask?)GetValue(SelectedTaskProperty);
        private set => SetValue(SelectedTaskPropertyKey, value);
    }

    #endregion

    /// <summary>The tasks, one per row in this order.</summary>
    public ObservableCollection<GanttTask> Tasks { get; } = [];

    public event EventHandler? SelectionChanged;

    /// <summary>Raised after the user moved or resized a task, or changed its progress.</summary>
    public event EventHandler<GanttTaskChangedEventArgs>? TaskChanged;

    /// <summary>Raised when a task is double-clicked.</summary>
    public event EventHandler<GanttTask>? TaskActivated;

    public void SelectTask(GanttTask? task)
    {
        var value = task != null && Tasks.Contains(task) ? task : null;
        if (ReferenceEquals(value, SelectedTask)) return;
        SelectedTask = value;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    #region Tasks

    private void OnTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (GanttTask task in e.OldItems)
            {
                task.PropertyChanged -= OnTaskChanged;
                task.DependsOn.CollectionChanged -= OnDependenciesChanged;
            }
        }
        if (e.NewItems != null)
        {
            foreach (GanttTask task in e.NewItems)
            {
                task.PropertyChanged += OnTaskChanged;
                task.DependsOn.CollectionChanged += OnDependenciesChanged;
            }
        }
        if (SelectedTask != null && !Tasks.Contains(SelectedTask)) SelectTask(null);
        UpdateScroll();
        InvalidateVisual();
    }

    private void OnTaskChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GanttTask.Start) or nameof(GanttTask.End)) UpdateScroll();
        InvalidateVisual();
    }

    private void OnDependenciesChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    #endregion

    #region Calendar geometry

    /// <summary>The first day of the calendar, a Monday.</summary>
    internal DateTime Origin
    {
        get
        {
            var start = ViewStart ?? (Tasks.Count == 0 ? DateTime.Today : Tasks.Min(t => t.Start).AddDays(-7));
            var back = ((int)start.DayOfWeek + 6) % 7;
            return start.Date.AddDays(-back);
        }
    }

    private DateTime LastDay => Tasks.Count == 0 ? Origin.AddDays(60) : Tasks.Max(t => t.IsMilestone ? t.Start.AddDays(1) : t.End);

    private double ContentWidth => ((LastDay - Origin).TotalDays + 21) * DayWidth;

    private double ContentHeight => Tasks.Count * RowHeight;

    private double BodyWidth => Math.Max(1, ActualWidth - NameColumnWidth - (_vScroll?.Visibility == Visibility.Visible ? ScrollSize : 0));

    private double BodyHeight => Math.Max(1, ActualHeight - HeaderHeight - (_hScroll?.Visibility == Visibility.Visible ? ScrollSize : 0));

    /// <summary>The x position of the start of <paramref name="date"/> in the control.</summary>
    internal double XOf(DateTime date) => NameColumnWidth + (date.Date - Origin).TotalDays * DayWidth - _hOffset;

    internal DateTime DateAtX(double x) => Origin.AddDays(Math.Floor((x - NameColumnWidth + _hOffset) / DayWidth));

    internal double RowTop(int index) => HeaderHeight + index * RowHeight - _vOffset;

    private double BarTop(int index) => RowTop(index) + RowHeight * 0.2;

    private double BarHeight => RowHeight * 0.52;

    internal Rect BarRect(GanttTask task)
    {
        var index = Tasks.IndexOf(task);
        if (task.IsMilestone)
        {
            var size = BarHeight + 4;
            var center = XOf(task.Start) + DayWidth / 2;
            return new Rect(center - size / 2, RowTop(index) + (RowHeight - size) / 2, size, size);
        }
        var left = XOf(task.Start);
        return new Rect(left, BarTop(index), Math.Max(4, (task.End - task.Start).TotalDays * DayWidth), BarHeight);
    }

    private Brush BrushOf(GanttTask task) => task.Brush ?? BarBrush;

    #endregion

    #region Scrolling

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_hScroll != null) _hScroll.ValueChanged -= OnHScroll;
        if (_vScroll != null) _vScroll.ValueChanged -= OnVScroll;
        _hScroll = GetTemplateChild("PART_HScroll") as ScrollBar;
        _vScroll = GetTemplateChild("PART_VScroll") as ScrollBar;
        if (_hScroll != null) _hScroll.ValueChanged += OnHScroll;
        if (_vScroll != null) _vScroll.ValueChanged += OnVScroll;
        UpdateScroll();
    }

    private void OnHScroll(object sender, RoutedPropertyChangedEventArgs<double> e) => SetHorizontalOffset(e.NewValue);

    private void OnVScroll(object sender, RoutedPropertyChangedEventArgs<double> e) => SetVerticalOffset(e.NewValue);

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateScroll();
    }

    /// <summary>The horizontal scroll position in pixels.</summary>
    public double HorizontalOffset => _hOffset;

    public double VerticalOffset => _vOffset;

    public void SetHorizontalOffset(double value)
    {
        var max = Math.Max(0, ContentWidth - BodyWidth);
        value = Math.Clamp(value, 0, max);
        if (Math.Abs(value - _hOffset) < 0.01) return;
        _hOffset = value;
        if (_hScroll != null && Math.Abs(_hScroll.Value - value) > 0.01) _hScroll.Value = value;
        InvalidateVisual();
    }

    public void SetVerticalOffset(double value)
    {
        var max = Math.Max(0, ContentHeight - BodyHeight);
        value = Math.Clamp(value, 0, max);
        if (Math.Abs(value - _vOffset) < 0.01) return;
        _vOffset = value;
        if (_vScroll != null && Math.Abs(_vScroll.Value - value) > 0.01) _vScroll.Value = value;
        InvalidateVisual();
    }

    private void UpdateScroll()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        // the bars decide whether they are needed, so work out the need of both together
        var needV = ContentHeight > ActualHeight - HeaderHeight;
        var needH = ContentWidth > ActualWidth - NameColumnWidth - (needV ? ScrollSize : 0);
        needV = ContentHeight > ActualHeight - HeaderHeight - (needH ? ScrollSize : 0);

        if (_vScroll != null)
        {
            _vScroll.Visibility = needV ? Visibility.Visible : Visibility.Collapsed;
            _vScroll.Margin = new Thickness(0, HeaderHeight, 0, needH ? ScrollSize : 0);
        }
        if (_hScroll != null)
        {
            _hScroll.Visibility = needH ? Visibility.Visible : Visibility.Collapsed;
            _hScroll.Margin = new Thickness(NameColumnWidth, 0, needV ? ScrollSize : 0, 0);
        }

        var maxH = Math.Max(0, ContentWidth - BodyWidth);
        var maxV = Math.Max(0, ContentHeight - BodyHeight);
        if (_hScroll != null)
        {
            _hScroll.Maximum = maxH;
            _hScroll.ViewportSize = BodyWidth;
            _hScroll.LargeChange = BodyWidth * 0.8;
            _hScroll.SmallChange = DayWidth * 3;
        }
        if (_vScroll != null)
        {
            _vScroll.Maximum = maxV;
            _vScroll.ViewportSize = BodyHeight;
            _vScroll.LargeChange = BodyHeight * 0.8;
            _vScroll.SmallChange = RowHeight;
        }
        _hOffset = Math.Clamp(_hOffset, 0, maxH);
        _vOffset = Math.Clamp(_vOffset, 0, maxV);
        if (_hScroll != null) _hScroll.Value = _hOffset;
        if (_vScroll != null) _vScroll.Value = _vOffset;
        InvalidateVisual();
    }

    /// <summary>Scrolls so that <paramref name="date"/> is at the left edge of the calendar.</summary>
    public void ScrollToDate(DateTime date) => SetHorizontalOffset((date.Date - Origin).TotalDays * DayWidth);

    /// <summary>Scrolls to the start of the task and makes its row visible.</summary>
    public void ScrollToTask(GanttTask task)
    {
        var index = Tasks.IndexOf(task);
        if (index < 0) return;
        ScrollToDate(task.Start.AddDays(-2));
        if (index * RowHeight < _vOffset) SetVerticalOffset(index * RowHeight);
        else if ((index + 1) * RowHeight > _vOffset + BodyHeight) SetVerticalOffset((index + 1) * RowHeight - BodyHeight);
    }

    /// <summary>Zooms so that all tasks fit into the width of the chart.</summary>
    public void FitAll()
    {
        if (Tasks.Count == 0) return;
        var days = (Tasks.Max(t => t.End) - Tasks.Min(t => t.Start)).TotalDays + 4;
        DayWidth = Math.Max(3, BodyWidth / Math.Max(1, days));
        ScrollToDate(Tasks.Min(t => t.Start).AddDays(-2));
    }

    public void ZoomIn() => Zoom(1.25, null);

    public void ZoomOut() => Zoom(0.8, null);

    internal void Zoom(double factor, double? anchorX)
    {
        var x = anchorX ?? (NameColumnWidth + BodyWidth / 2);
        var date = (x - NameColumnWidth + _hOffset) / DayWidth;
        DayWidth *= factor;
        SetHorizontalOffset(date * DayWidth - (x - NameColumnWidth));
    }

    #endregion

    #region Rendering

    private FormattedText Text(string text, Brush brush, double size, bool bold = false, double maxWidth = double.PositiveInfinity)
    {
        var typeface = new Typeface(FontFamily, FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (!double.IsInfinity(maxWidth))
        {
            formatted.MaxTextWidth = Math.Max(1, maxWidth);
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }
        return formatted;
    }

    private DateTime TodayDate => (Today ?? DateTime.Today).Date;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 80 || height < HeaderHeight + 20) return;

        var text = Foreground ?? Brushes.Black;
        var grid = GridBrush;
        var bodyLeft = NameColumnWidth;
        var bodyRight = width - (_vScroll?.Visibility == Visibility.Visible ? ScrollSize : 0);
        var bodyBottom = height - (_hScroll?.Visibility == Visibility.Visible ? ScrollSize : 0);

        dc.DrawRoundedRectangle(Background ?? Brushes.Transparent, new Pen(BorderBrush ?? grid, 1), new Rect(0.5, 0.5, width - 1, height - 1), 6, 6);

        // the calendar body: weekends, day lines, today
        dc.PushClip(new RectangleGeometry(new Rect(bodyLeft, HeaderHeight, Math.Max(0, bodyRight - bodyLeft), Math.Max(0, bodyBottom - HeaderHeight))));
        DrawBody(dc, bodyLeft, bodyRight, bodyBottom, grid, text);
        dc.Pop();

        // header
        DrawHeader(dc, bodyLeft, bodyRight, text, grid);

        // names
        if (bodyLeft > 0)
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, HeaderHeight, bodyLeft, Math.Max(0, bodyBottom - HeaderHeight))));
            DrawNames(dc, bodyLeft, bodyBottom, text, grid);
            dc.Pop();
        }

        dc.DrawLine(new Pen(grid, 1), new Point(bodyLeft, 0), new Point(bodyLeft, bodyBottom));
        dc.DrawLine(new Pen(grid, 1), new Point(0, HeaderHeight), new Point(bodyRight, HeaderHeight));
    }

    private void DrawBody(DrawingContext dc, double left, double right, double bottom, Brush grid, Brush text)
    {
        var first = DateAtX(left);
        var last = DateAtX(right).AddDays(1);
        var gridPen = new Pen(grid, 1);

        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var x = XOf(day);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday && DayWidth >= 5)
            {
                dc.DrawRectangle(WeekendBrush, null, new Rect(x, HeaderHeight, DayWidth, Math.Max(0, bottom - HeaderHeight)));
            }
            if (DayWidth >= 14 || day.DayOfWeek == DayOfWeek.Monday)
            {
                dc.PushOpacity(DayWidth >= 14 && day.DayOfWeek != DayOfWeek.Monday ? 0.35 : 0.8);
                dc.DrawLine(gridPen, new Point(x, HeaderHeight), new Point(x, bottom));
                dc.Pop();
            }
        }

        // rows
        var firstRow = Math.Max(0, (int)(_vOffset / RowHeight));
        var lastRow = Math.Min(Tasks.Count - 1, (int)((_vOffset + bottom) / RowHeight));
        for (var i = firstRow; i <= lastRow; i++)
        {
            var y = RowTop(i) + RowHeight;
            dc.PushOpacity(0.7);
            dc.DrawLine(gridPen, new Point(left, y), new Point(right, y));
            dc.Pop();
        }

        // dependencies below the bars
        if (ShowDependencies) DrawDependencies(dc, firstRow, lastRow);

        for (var i = firstRow; i <= lastRow; i++) DrawTask(dc, Tasks[i], text);

        if (ShowToday)
        {
            var x = XOf(TodayDate) + DayWidth / 2;
            if (x >= left && x <= right)
            {
                dc.DrawLine(new Pen(TodayBrush, 1.5), new Point(x, HeaderHeight), new Point(x, bottom));
            }
        }
    }

    private void DrawTask(DrawingContext dc, GanttTask task, Brush text)
    {
        var rect = BarRect(task);
        var brush = BrushOf(task);
        var selected = ReferenceEquals(task, SelectedTask);
        var hover = ReferenceEquals(task, _hover);

        if (task.IsMilestone)
        {
            var center = new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            var half = rect.Width / 2;
            var diamond = new StreamGeometry();
            using (var ctx = diamond.Open())
            {
                ctx.BeginFigure(new Point(center.X, center.Y - half), true, true);
                ctx.LineTo(new Point(center.X + half, center.Y), true, false);
                ctx.LineTo(new Point(center.X, center.Y + half), true, false);
                ctx.LineTo(new Point(center.X - half, center.Y), true, false);
            }
            diamond.Freeze();
            dc.DrawGeometry(brush, selected ? new Pen(text, 2) : null, diamond);
            DrawBarLabel(dc, task, new Rect(rect.Right + 6, rect.Top, 400, rect.Height), text, false);
            return;
        }

        var radius = Math.Min(5, rect.Height / 3);
        dc.DrawRoundedRectangle(brush, null, rect, radius, radius);
        if (task.Progress > 0)
        {
            var done = new Rect(rect.Left, rect.Top, rect.Width * task.Progress, rect.Height);
            dc.PushClip(new RectangleGeometry(rect, radius, radius));
            dc.PushOpacity(0.3);
            dc.DrawRectangle(Brushes.Black, null, done);
            dc.Pop();
            dc.Pop();
        }
        if (hover && !selected)
        {
            dc.PushOpacity(0.2);
            dc.DrawRoundedRectangle(Brushes.White, null, rect, radius, radius);
            dc.Pop();
        }
        if (selected) dc.DrawRoundedRectangle(null, new Pen(text, 2), rect, radius, radius);

        DrawBarLabel(dc, task, rect, Brushes.White, true);

        // the handle that sets the progress (under the bar, for the selected or hovered task)
        if (IsEditable && (selected || hover))
        {
            var x = rect.Left + rect.Width * task.Progress;
            var y = rect.Bottom + 1;
            var handle = new StreamGeometry();
            using (var ctx = handle.Open())
            {
                ctx.BeginFigure(new Point(x, y), true, true);
                ctx.LineTo(new Point(x - 4.5, y + 7), true, false);
                ctx.LineTo(new Point(x + 4.5, y + 7), true, false);
            }
            handle.Freeze();
            dc.DrawGeometry(text, null, handle);
        }
    }

    private void DrawBarLabel(DrawingContext dc, GanttTask task, Rect rect, Brush brush, bool inside)
    {
        if (string.IsNullOrEmpty(task.Name)) return;
        var label = Text(task.Name, brush, Math.Max(10, Math.Min(12.5, RowHeight * 0.34)), inside, inside ? rect.Width - 12 : 400);
        if (inside && rect.Width < 36) return;
        dc.DrawText(label, new Point(rect.Left + (inside ? 7 : 0), rect.Top + (rect.Height - label.Height) / 2));
    }

    private void DrawDependencies(DrawingContext dc, int firstRow, int lastRow)
    {
        var textBrush = Foreground ?? Brushes.Gray;
        for (var i = 0; i < Tasks.Count; i++)
        {
            var task = Tasks[i];
            foreach (var predecessor in task.DependsOn)
            {
                var from = Tasks.IndexOf(predecessor);
                if (from < 0 || (i < firstRow && from < firstRow) || (i > lastRow && from > lastRow)) continue;
                var a = BarRect(predecessor);
                var b = BarRect(task);
                var start = new Point(predecessor.IsMilestone ? a.Right : a.Right, a.Top + a.Height / 2);
                var end = new Point(b.Left, b.Top + b.Height / 2);
                var violated = task.Start < (predecessor.IsMilestone ? predecessor.Start.AddDays(1) : predecessor.End);
                var pen = new Pen(violated ? TodayBrush : textBrush, 1.3);

                var path = new StreamGeometry();
                using (var ctx = path.Open())
                {
                    var elbow = Math.Max(start.X + 8, Math.Min(end.X - 8, start.X + 8));
                    ctx.BeginFigure(start, false, false);
                    if (end.X >= start.X + 16)
                    {
                        ctx.LineTo(new Point(elbow, start.Y), true, false);
                        ctx.LineTo(new Point(elbow, end.Y), true, false);
                        ctx.LineTo(end, true, false);
                    }
                    else
                    {
                        // the successor starts before the predecessor ends: go around
                        var midY = (start.Y + end.Y) / 2 + (end.Y >= start.Y ? RowHeight * 0.45 : -RowHeight * 0.45);
                        ctx.LineTo(new Point(start.X + 8, start.Y), true, false);
                        ctx.LineTo(new Point(start.X + 8, midY), true, false);
                        ctx.LineTo(new Point(end.X - 8, midY), true, false);
                        ctx.LineTo(new Point(end.X - 8, end.Y), true, false);
                        ctx.LineTo(end, true, false);
                    }
                }
                path.Freeze();
                dc.PushOpacity(0.75);
                dc.DrawGeometry(null, pen, path);
                var head = new StreamGeometry();
                using (var ctx = head.Open())
                {
                    ctx.BeginFigure(end, true, true);
                    ctx.LineTo(new Point(end.X - 6, end.Y - 3.5), true, false);
                    ctx.LineTo(new Point(end.X - 6, end.Y + 3.5), true, false);
                }
                head.Freeze();
                dc.DrawGeometry(violated ? TodayBrush : textBrush, null, head);
                dc.Pop();
            }
        }
    }

    private void DrawHeader(DrawingContext dc, double left, double right, Brush text, Brush grid)
    {
        var surface = Background ?? Brushes.Transparent;
        dc.DrawRoundedRectangle(surface, null, new Rect(1, 1, Math.Max(0, right - 2), HeaderHeight - 1), 5, 5);
        dc.PushClip(new RectangleGeometry(new Rect(left, 0, Math.Max(0, right - left), HeaderHeight)));

        var first = DateAtX(left).AddDays(-1);
        var last = DateAtX(right).AddDays(1);
        var small = Foreground ?? Brushes.Gray;
        var smallPen = new Pen(grid, 1);
        var culture = CultureInfo.CurrentCulture;

        // top line: months
        var month = new DateTime(first.Year, first.Month, 1);
        while (month <= last)
        {
            var next = month.AddMonths(1);
            var x0 = Math.Max(XOf(month), left);
            var x1 = XOf(next);
            if (x1 > left)
            {
                var label = Text(month.ToString(DayWidth > 5 ? "MMMM yyyy" : "MMM yy", culture), text, 12, true, Math.Max(10, x1 - x0 - 12));
                dc.DrawText(label, new Point(x0 + 6, 5));
                dc.DrawLine(smallPen, new Point(XOf(month), 0), new Point(XOf(month), 24));
            }
            month = next;
        }
        dc.DrawLine(smallPen, new Point(left, 24), new Point(right, 24));

        // bottom line: days when they fit, else the Monday of each week
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var x = XOf(day);
            if (DayWidth >= 20)
            {
                var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                var label = Text(day.Day.ToString(culture), weekend ? small : text, 11, day.Date == TodayDate, DayWidth);
                dc.DrawText(label, new Point(x + (DayWidth - label.WidthIncludingTrailingWhitespace) / 2, 28));
                if (DayWidth >= 34)
                {
                    var name = Text(culture.DateTimeFormat.GetShortestDayName(day.DayOfWeek), small, 9.5);
                    dc.DrawText(name, new Point(x + (DayWidth - name.Width) / 2, 38));
                }
            }
            else if (day.DayOfWeek == DayOfWeek.Monday && DayWidth >= 6)
            {
                var label = Text(day.Day.ToString(culture), small, 10.5, false, DayWidth * 7);
                dc.DrawText(label, new Point(x + 3, 31));
            }
        }
        dc.Pop();
    }

    private void DrawNames(DrawingContext dc, double width, double bottom, Brush text, Brush grid)
    {
        var small = Foreground ?? Brushes.Gray;
        var firstRow = Math.Max(0, (int)(_vOffset / RowHeight));
        var lastRow = Math.Min(Tasks.Count - 1, (int)((_vOffset + bottom) / RowHeight));
        for (var i = firstRow; i <= lastRow; i++)
        {
            var task = Tasks[i];
            var top = RowTop(i);
            if (ReferenceEquals(task, SelectedTask))
            {
                dc.PushOpacity(0.12);
                dc.DrawRectangle(BarBrush, null, new Rect(1, top, width - 1, RowHeight));
                dc.Pop();
            }
            var name = Text(task.Name, text, 12.5, false, width - 20);
            var range = Text($"{task.Start:d MMM} - {task.End.AddDays(-1):d MMM}" + (string.IsNullOrEmpty(task.Assignee) ? "" : "  " + task.Assignee), small, 10.5, false, width - 20);
            var total = name.Height + (RowHeight >= 34 ? range.Height : 0);
            var y = top + (RowHeight - total) / 2;
            dc.DrawText(name, new Point(10, y));
            if (RowHeight >= 34) dc.DrawText(range, new Point(10, y + name.Height));
            dc.PushOpacity(0.7);
            dc.DrawLine(new Pen(grid, 1), new Point(0, top + RowHeight), new Point(width, top + RowHeight));
            dc.Pop();
        }
    }

    #endregion

    #region Input

    private (GanttTask? Task, DragMode Mode) HitTest(Point point)
    {
        if (point.Y < HeaderHeight) return (null, DragMode.None);

        if (point.X < NameColumnWidth)
        {
            var row = (int)Math.Floor((point.Y - HeaderHeight + _vOffset) / RowHeight);
            return (row >= 0 && row < Tasks.Count ? Tasks[row] : null, DragMode.None);
        }

        for (var i = 0; i < Tasks.Count; i++)
        {
            var task = Tasks[i];
            var rect = BarRect(task);
            if (task.IsMilestone)
            {
                if (rect.Contains(point)) return (task, DragMode.Move);
                continue;
            }

            // the progress handle under the bar
            if ((ReferenceEquals(task, SelectedTask) || ReferenceEquals(task, _hover)) && IsEditable)
            {
                var hx = rect.Left + rect.Width * task.Progress;
                if (Math.Abs(point.X - hx) <= 6 && point.Y >= rect.Bottom && point.Y <= rect.Bottom + 9) return (task, DragMode.Progress);
            }

            var hit = new Rect(rect.Left - 2, rect.Top, rect.Width + 4, rect.Height);
            if (!hit.Contains(point)) continue;
            if (!IsEditable) return (task, DragMode.None);
            if (rect.Width > EdgeGrip * 3 && point.X <= rect.Left + EdgeGrip) return (task, DragMode.ResizeStart);
            if (rect.Width > EdgeGrip * 3 && point.X >= rect.Right - EdgeGrip) return (task, DragMode.ResizeEnd);
            return (task, DragMode.Move);
        }

        // a click in the empty part of a row selects that row's task
        var rowIndex = (int)Math.Floor((point.Y - HeaderHeight + _vOffset) / RowHeight);
        return (rowIndex >= 0 && rowIndex < Tasks.Count ? Tasks[rowIndex] : null, DragMode.None);
    }

    internal bool PointerDown(Point point, int clickCount)
    {
        var (task, mode) = HitTest(point);
        SelectTask(task);
        if (task == null) return false;
        if (clickCount >= 2)
        {
            TaskActivated?.Invoke(this, task);
            return false;
        }
        if (mode == DragMode.None || !IsEditable) return false;

        _drag = mode;
        _dragTask = task;
        _downPoint = point;
        _origStart = task.Start;
        _origEnd = task.End;
        _origProgress = task.Progress;
        _changed = false;
        return true;
    }

    internal void PointerMove(Point point)
    {
        if (_drag == DragMode.None || _dragTask is not { } task)
        {
            var hit = HitTest(point);
            var hover = hit.Task != null && point.X >= NameColumnWidth ? hit.Task : null;
            if (!ReferenceEquals(hover, _hover))
            {
                _hover = hover;
                InvalidateVisual();
            }
            Cursor = hit.Mode switch
            {
                DragMode.ResizeStart or DragMode.ResizeEnd => Cursors.SizeWE,
                DragMode.Move => Cursors.SizeAll,
                DragMode.Progress => Cursors.Hand,
                _ => null,
            };
            return;
        }

        var days = (int)Math.Round((point.X - _downPoint.X) / DayWidth);
        switch (_drag)
        {
            case DragMode.Move:
                task.Start = _origStart.AddDays(days);
                task.End = _origEnd.AddDays(days);
                break;
            case DragMode.ResizeStart:
                task.Start = new DateTime(Math.Min(_origStart.AddDays(days).Ticks, _origEnd.AddDays(-1).Ticks));
                break;
            case DragMode.ResizeEnd:
                task.End = new DateTime(Math.Max(_origEnd.AddDays(days).Ticks, _origStart.AddDays(1).Ticks));
                break;
            case DragMode.Progress:
                var rect = BarRect(task);
                task.Progress = Math.Round((point.X - rect.Left) / Math.Max(1, rect.Width) * 20) / 20;
                break;
        }
        _changed = task.Start != _origStart || task.End != _origEnd || Math.Abs(task.Progress - _origProgress) > 1e-9;
    }

    internal void PointerUp()
    {
        if (_drag == DragMode.None) return;
        var mode = _drag;
        var task = _dragTask;
        _drag = DragMode.None;
        _dragTask = null;
        if (task != null && _changed)
        {
            var change = mode switch
            {
                DragMode.Progress => GanttChange.ProgressChanged,
                DragMode.Move => GanttChange.Moved,
                _ => GanttChange.Resized,
            };
            TaskChanged?.Invoke(this, new GanttTaskChangedEventArgs(task, change, _origStart, _origEnd, _origProgress));
        }
        _changed = false;
    }

    /// <summary>Puts the task back as it was before the drag and ends it (Escape does this).</summary>
    internal void CancelDrag()
    {
        if (_drag == DragMode.None || _dragTask is not { } task) return;
        task.Start = _origStart;
        task.End = _origEnd;
        task.Progress = _origProgress;
        _drag = DragMode.None;
        _dragTask = null;
        _changed = false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (PointerDown(e.GetPosition(this), e.ClickCount)) CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        PointerMove(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        PointerUp();
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        PointerUp();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != null && _drag == DragMode.None)
        {
            _hover = null;
            Cursor = null;
            InvalidateVisual();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var modifiers = Keyboard.Modifiers;
        if ((modifiers & ModifierKeys.Control) != 0)
        {
            Zoom(e.Delta > 0 ? 1.2 : 1 / 1.2, e.GetPosition(this).X);
        }
        else if ((modifiers & ModifierKeys.Shift) != 0 || (ContentHeight <= BodyHeight && ContentWidth > BodyWidth))
        {
            SetHorizontalOffset(_hOffset - Math.Sign(e.Delta) * DayWidth * 3);
        }
        else if (ContentHeight > BodyHeight)
        {
            SetVerticalOffset(_vOffset - Math.Sign(e.Delta) * RowHeight * 2);
        }
        else
        {
            return;
        }
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && _drag != DragMode.None)
        {
            CancelDrag();
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
        }
        else if (e.Key is Key.Down or Key.Up && Tasks.Count > 0)
        {
            var index = SelectedTask == null ? -1 : Tasks.IndexOf(SelectedTask);
            index = Math.Clamp(index + (e.Key == Key.Down ? 1 : -1), 0, Tasks.Count - 1);
            SelectTask(Tasks[index]);
            ScrollToTask(Tasks[index]);
            e.Handled = true;
        }
    }

    #endregion
}
