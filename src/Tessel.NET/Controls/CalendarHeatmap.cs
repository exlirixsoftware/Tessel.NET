using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Tessel.NET.Controls;

public sealed class HeatmapDayEventArgs(DateTime date, double value) : RoutedEventArgs
{
    public DateTime Date { get; } = date;

    public double Value { get; } = value;
}

/// <summary>
/// A year of days as a grid of colored squares, one column per week (the contribution graph of GitHub): activity, commits, sales, workouts.
/// Give it <see cref="Data"/> (a value for each date that has one) and the darker the square the higher the value. Hover shows the day, click
/// selects it.
/// </summary>
public class CalendarHeatmap : Control
{
    private const double DayLabelWidth = 30;
    private const double MonthLabelHeight = 18;
    private const double LegendHeight = 26;

    private DateTime? _hover;

    static CalendarHeatmap()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CalendarHeatmap), new FrameworkPropertyMetadata(typeof(CalendarHeatmap)));
        FocusableProperty.OverrideMetadata(typeof(CalendarHeatmap), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(IDictionary<DateTime, double>), typeof(CalendarHeatmap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EndDateProperty = DependencyProperty.Register(
        nameof(EndDate), typeof(DateTime?), typeof(CalendarHeatmap), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WeekCountProperty = DependencyProperty.Register(
        nameof(WeekCount), typeof(int), typeof(CalendarHeatmap),
        new FrameworkPropertyMetadata(53, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, null, (_, v) => Math.Clamp((int)v, 1, 105)));

    public static readonly DependencyProperty FirstDayOfWeekProperty = DependencyProperty.Register(
        nameof(FirstDayOfWeek), typeof(DayOfWeek), typeof(CalendarHeatmap), new FrameworkPropertyMetadata(DayOfWeek.Monday, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CellSizeProperty = DependencyProperty.Register(
        nameof(CellSize), typeof(double), typeof(CalendarHeatmap),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, null, (_, v) => Math.Max(4, (double)v)));

    public static readonly DependencyProperty CellSpacingProperty = DependencyProperty.Register(
        nameof(CellSpacing), typeof(double), typeof(CalendarHeatmap),
        new FrameworkPropertyMetadata(3d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, null, (_, v) => Math.Max(0, (double)v)));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(CalendarHeatmap), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LevelsProperty = DependencyProperty.Register(
        nameof(Levels), typeof(int), typeof(CalendarHeatmap), new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender, null, (_, v) => Math.Clamp((int)v, 2, 8)));

    public static readonly DependencyProperty ShowMonthLabelsProperty = DependencyProperty.Register(
        nameof(ShowMonthLabels), typeof(bool), typeof(CalendarHeatmap), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowDayLabelsProperty = DependencyProperty.Register(
        nameof(ShowDayLabels), typeof(bool), typeof(CalendarHeatmap), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowLegendProperty = DependencyProperty.Register(
        nameof(ShowLegend), typeof(bool), typeof(CalendarHeatmap), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ToolTipFormatProperty = DependencyProperty.Register(
        nameof(ToolTipFormat), typeof(string), typeof(CalendarHeatmap), new PropertyMetadata("{0:ddd, d MMM yyyy}: {1:0.##}"));

    public static readonly DependencyProperty LevelBrushProperty = DependencyProperty.Register(
        nameof(LevelBrush), typeof(Brush), typeof(CalendarHeatmap), new FrameworkPropertyMetadata(Brushes.SeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EmptyBrushProperty = DependencyProperty.Register(
        nameof(EmptyBrush), typeof(Brush), typeof(CalendarHeatmap), new FrameworkPropertyMetadata(Brushes.Gainsboro, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime?), typeof(CalendarHeatmap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly RoutedEvent DayClickedEvent = EventManager.RegisterRoutedEvent(
        nameof(DayClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CalendarHeatmap));

    /// <summary>The value of each day that has one (days that are missing, or zero, are drawn empty). Replace the dictionary to update the graph.</summary>
    public IDictionary<DateTime, double>? Data
    {
        get => (IDictionary<DateTime, double>?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>The last day shown (null = today).</summary>
    public DateTime? EndDate
    {
        get => (DateTime?)GetValue(EndDateProperty);
        set => SetValue(EndDateProperty, value);
    }

    /// <summary>The number of columns (weeks); 53 shows a whole year.</summary>
    public int WeekCount
    {
        get => (int)GetValue(WeekCountProperty);
        set => SetValue(WeekCountProperty, value);
    }

    public DayOfWeek FirstDayOfWeek
    {
        get => (DayOfWeek)GetValue(FirstDayOfWeekProperty);
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    public double CellSize
    {
        get => (double)GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public double CellSpacing
    {
        get => (double)GetValue(CellSpacingProperty);
        set => SetValue(CellSpacingProperty, value);
    }

    /// <summary>The value of the darkest shade (NaN = the highest value of <see cref="Data"/>).</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>The number of shades above "empty".</summary>
    public int Levels
    {
        get => (int)GetValue(LevelsProperty);
        set => SetValue(LevelsProperty, value);
    }

    public bool ShowMonthLabels
    {
        get => (bool)GetValue(ShowMonthLabelsProperty);
        set => SetValue(ShowMonthLabelsProperty, value);
    }

    public bool ShowDayLabels
    {
        get => (bool)GetValue(ShowDayLabelsProperty);
        set => SetValue(ShowDayLabelsProperty, value);
    }

    /// <summary>The "Less to More" scale under the grid.</summary>
    public bool ShowLegend
    {
        get => (bool)GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    /// <summary>The tooltip of a day: {0} is the date, {1} its value.</summary>
    public string ToolTipFormat
    {
        get => (string)GetValue(ToolTipFormatProperty);
        set => SetValue(ToolTipFormatProperty, value);
    }

    /// <summary>The color of the darkest shade; lighter shades are the same color made more transparent.</summary>
    public Brush LevelBrush
    {
        get => (Brush)GetValue(LevelBrushProperty);
        set => SetValue(LevelBrushProperty, value);
    }

    public Brush EmptyBrush
    {
        get => (Brush)GetValue(EmptyBrushProperty);
        set => SetValue(EmptyBrushProperty, value);
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <summary>Raised when a day is clicked (<see cref="HeatmapDayEventArgs"/>).</summary>
    public event RoutedEventHandler DayClicked
    {
        add => AddHandler(DayClickedEvent, value);
        remove => RemoveHandler(DayClickedEvent, value);
    }

    #region Layout

    private double Pitch => CellSize + CellSpacing;

    private double Left => ShowDayLabels ? DayLabelWidth : 0;

    private double Top => ShowMonthLabels ? MonthLabelHeight : 0;

    /// <summary>The first day of the first column.</summary>
    internal DateTime FirstDay
    {
        get
        {
            var end = (EndDate ?? DateTime.Today).Date;
            var back = ((int)end.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
            return end.AddDays(-back - 7 * (WeekCount - 1));
        }
    }

    internal DateTime LastDay => (EndDate ?? DateTime.Today).Date;

    internal (int Column, int Row) CellOf(DateTime date)
    {
        var days = (date.Date - FirstDay).Days;
        return (days / 7, days % 7);
    }

    internal DateTime DateOf(int column, int row) => FirstDay.AddDays(column * 7 + row);

    internal Rect CellRect(int column, int row) => new(Left + column * Pitch, Top + row * Pitch, CellSize, CellSize);

    protected override Size MeasureOverride(Size availableSize)
        => new(Left + WeekCount * Pitch - CellSpacing, Top + 7 * Pitch - CellSpacing + (ShowLegend ? LegendHeight : 0));

    internal DateTime? DateAt(Point point)
    {
        var column = (int)Math.Floor((point.X - Left) / Pitch);
        var row = (int)Math.Floor((point.Y - Top) / Pitch);
        if (column < 0 || column >= WeekCount || row < 0 || row > 6) return null;
        var inCellX = point.X - Left - column * Pitch;
        var inCellY = point.Y - Top - row * Pitch;
        if (inCellX > CellSize || inCellY > CellSize) return null;
        var date = DateOf(column, row);
        return date > LastDay ? null : date;
    }

    #endregion

    #region Rendering

    private FormattedText Text(string text, double size)
    {
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, Foreground ?? Brushes.Gray, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private double MaxValue()
    {
        if (!double.IsNaN(Maximum)) return Maximum;
        var max = 0d;
        if (Data != null)
        {
            foreach (var value in Data.Values) max = Math.Max(max, value);
        }
        return max;
    }

    /// <summary>0 for an empty day, otherwise 1 to <see cref="Levels"/>.</summary>
    internal int LevelOf(double value, double max)
    {
        if (value <= 0 || max <= 0) return 0;
        return Math.Clamp((int)Math.Ceiling(value / max * Levels), 1, Levels);
    }

    internal double ValueOf(DateTime date)
    {
        if (Data == null) return 0;
        return Data.TryGetValue(date.Date, out var value) ? value : 0;
    }

    private void DrawLevel(DrawingContext dc, Rect rect, int level)
    {
        var radius = Math.Min(3, CellSize / 4);
        if (level == 0)
        {
            dc.DrawRoundedRectangle(EmptyBrush, null, rect, radius, radius);
            return;
        }
        dc.PushOpacity(0.28 + 0.72 * level / Levels);
        dc.DrawRoundedRectangle(LevelBrush, null, rect, radius, radius);
        dc.Pop();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var max = MaxValue();
        var first = FirstDay;
        var last = LastDay;
        var size = Math.Max(9, Math.Min(11, CellSize));

        if (ShowDayLabels)
        {
            for (var row = 1; row < 7; row += 2)
            {
                var day = (DayOfWeek)(((int)FirstDayOfWeek + row) % 7);
                var label = Text(CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day), size);
                dc.DrawText(label, new Point(0, Top + row * Pitch + (CellSize - label.Height) / 2));
            }
        }

        var lastMonthLabel = -100.0;
        for (var column = 0; column < WeekCount; column++)
        {
            if (ShowMonthLabels)
            {
                var month = first.AddDays(column * 7).Month;
                var x = Left + column * Pitch;
                if ((column == 0 || month != first.AddDays((column - 1) * 7).Month) && x - lastMonthLabel > 26)
                {
                    dc.DrawText(Text(CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month), size), new Point(x, 0));
                    lastMonthLabel = x;
                }
            }

            for (var row = 0; row < 7; row++)
            {
                var date = first.AddDays(column * 7 + row);
                if (date > last) break;
                var rect = CellRect(column, row);
                DrawLevel(dc, rect, LevelOf(ValueOf(date), max));
                if (SelectedDate?.Date == date)
                {
                    dc.DrawRoundedRectangle(null, new Pen(Foreground ?? Brushes.Black, 1.5), rect, 3, 3);
                }
                else if (_hover == date)
                {
                    dc.DrawRoundedRectangle(null, new Pen(Foreground ?? Brushes.Gray, 1), rect, 3, 3);
                }
            }
        }

        if (ShowLegend)
        {
            var y = Top + 7 * Pitch - CellSpacing + 8;
            var less = Text("Less", size);
            var more = Text("More", size);
            var legendWidth = less.Width + 6 + (Levels + 1) * (CellSize + 2) + 4 + more.Width;
            var x = Math.Max(Left, Left + WeekCount * Pitch - CellSpacing - legendWidth);
            dc.DrawText(less, new Point(x, y + (CellSize - less.Height) / 2));
            x += less.Width + 6;
            for (var level = 0; level <= Levels; level++)
            {
                DrawLevel(dc, new Rect(x, y, CellSize, CellSize), level);
                x += CellSize + 2;
            }
            dc.DrawText(more, new Point(x + 2, y + (CellSize - more.Height) / 2));
        }
    }

    #endregion

    #region Input

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var date = DateAt(e.GetPosition(this));
        if (date == _hover) return;
        _hover = date;
        ToolTip = date == null ? null : string.Format(CultureInfo.CurrentCulture, ToolTipFormat, date.Value, ValueOf(date.Value));
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover == null) return;
        _hover = null;
        ToolTip = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var date = DateAt(e.GetPosition(this));
        if (date == null) return;
        SelectedDate = date;
        RaiseEvent(new HeatmapDayEventArgs(date.Value, ValueOf(date.Value)) { RoutedEvent = DayClickedEvent });
        e.Handled = true;
    }

    #endregion
}
