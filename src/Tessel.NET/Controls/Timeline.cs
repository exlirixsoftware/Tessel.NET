using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Tessel.NET.Controls;

/// <summary>How the entries of a <see cref="Timeline"/> are arranged around the line.</summary>
public enum TimelineLayout
{
    /// <summary>All entries on one side of the line (to the right of a vertical line, below a horizontal one); timestamps on the other side.</summary>
    Standard,

    /// <summary>Entries alternate between the two sides of the line.</summary>
    Alternate,
}

/// <summary>The side of the line a <see cref="TimelineItem"/> shows its content on.</summary>
public enum TimelineSide
{
    /// <summary>Left of a vertical line, above a horizontal line.</summary>
    Start,

    /// <summary>Right of a vertical line, below a horizontal line.</summary>
    End,
}

/// <summary>
/// A chronological list of events (an activity feed, order tracking, a project history). Each <see cref="TimelineItem"/> has a marker on
/// a connecting line, a header, a description and an optional timestamp. Unlike <see cref="Stepper"/> it has no "current step": every
/// entry is a fact, and entries that have not happened yet can be marked <see cref="TimelineItem.IsPending"/>.
/// <code>
/// &lt;tessel:Timeline Layout="Alternate"&gt;
///     &lt;tessel:TimelineItem Header="Order placed" Timestamp="10:02" Kind="Success" Icon="{tessel:Glyph Shop}" /&gt;
///     &lt;tessel:TimelineItem Header="Shipped" Timestamp="Tomorrow" IsPending="True" /&gt;
/// &lt;/tessel:Timeline&gt;
/// </code>
/// With <c>ItemsSource</c> the data item becomes the content of each entry; bind the other properties with an <c>ItemContainerStyle</c>.
/// </summary>
[TemplatePart(Name = "PART_Scroller", Type = typeof(ScrollViewer))]
public class Timeline : ItemsControl
{
    private ScrollViewer? _scroller;

    static Timeline()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(typeof(Timeline)));
        FocusableProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(false));
    }

    /// <summary>Vertical (default) or horizontal line.</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(Timeline), new PropertyMetadata(Orientation.Vertical, OnLayoutChanged));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout), typeof(TimelineLayout), typeof(Timeline), new PropertyMetadata(TimelineLayout.Standard, OnLayoutChanged));

    public TimelineLayout Layout
    {
        get => (TimelineLayout)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>
    /// Width of each entry of a horizontal timeline (default 200). Use <see cref="double.NaN"/> to size entries to their content.
    /// The timeline scrolls horizontally when the entries do not fit.
    /// </summary>
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(Timeline), new PropertyMetadata(200d, OnLayoutChanged));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (_scroller != null) _scroller.PreviewMouseWheel -= OnScrollerWheel;
        base.OnApplyTemplate();
        _scroller = GetTemplateChild("PART_Scroller") as ScrollViewer;
        if (_scroller != null) _scroller.PreviewMouseWheel += OnScrollerWheel;
    }

    // The mouse wheel scrolls a horizontal timeline sideways.
    private void OnScrollerWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scroller is not { ScrollableWidth: > 0 } scroller) return;
        scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is TimelineItem;

    protected override DependencyObject GetContainerForItemOverride() => new TimelineItem();

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        ScheduleUpdate();
    }

    protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        ScheduleUpdate();
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Timeline)d).ScheduleUpdate();

    private void ScheduleUpdate() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateItems);

    internal void UpdateItems()
    {
        var count = Items.Count;
        for (var i = 0; i < count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is not TimelineItem item) continue;

            item.Index = i + 1;
            item.IsFirst = i == 0;
            item.IsLast = i == count - 1;
            item.Orientation = Orientation;
            item.Layout = Layout;
            item.Side = Layout == TimelineLayout.Alternate && i % 2 == 1 ? TimelineSide.Start : TimelineSide.End;

            if (Orientation == Orientation.Horizontal && !double.IsNaN(ItemWidth)) item.Width = ItemWidth;
            else item.ClearValue(FrameworkElement.WidthProperty);
        }
    }
}

/// <summary>One entry of a <see cref="Timeline"/>. The content (if any) is the description shown below the header.</summary>
public class TimelineItem : ContentControl
{
    static TimelineItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TimelineItem), new FrameworkPropertyMetadata(typeof(TimelineItem)));
        FocusableProperty.OverrideMetadata(typeof(TimelineItem), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(TimelineItem), new FrameworkPropertyMetadata(false));
    }

    /// <summary>The title of the entry.</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(TimelineItem), new PropertyMetadata(null));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>When it happened: any text, a <see cref="DateTime"/> (see <see cref="TimestampStringFormat"/>) or an element. Shown on the opposite side of the content.</summary>
    public static readonly DependencyProperty TimestampProperty = DependencyProperty.Register(
        nameof(Timestamp), typeof(object), typeof(TimelineItem), new PropertyMetadata(null));

    public object? Timestamp
    {
        get => GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    /// <summary>Format applied to <see cref="Timestamp"/> when it is a date, e.g. <c>"d MMM, HH:mm"</c>.</summary>
    public static readonly DependencyProperty TimestampStringFormatProperty = DependencyProperty.Register(
        nameof(TimestampStringFormat), typeof(string), typeof(TimelineItem), new PropertyMetadata(null));

    public string? TimestampStringFormat
    {
        get => (string?)GetValue(TimestampStringFormatProperty);
        set => SetValue(TimestampStringFormatProperty, value);
    }

    /// <summary>A glyph (use <c>{tessel:Glyph ...}</c>) or any element shown inside the marker. Without an icon the marker is a dot.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(TimelineItem), new PropertyMetadata(null));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Colors the marker (accent by default).</summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(BadgeKind), typeof(TimelineItem), new PropertyMetadata(BadgeKind.Accent));

    public BadgeKind Kind
    {
        get => (BadgeKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Marks an entry that has not happened yet: a hollow, faded marker and line.</summary>
    public static readonly DependencyProperty IsPendingProperty = DependencyProperty.Register(
        nameof(IsPending), typeof(bool), typeof(TimelineItem), new PropertyMetadata(false));

    public bool IsPending
    {
        get => (bool)GetValue(IsPendingProperty);
        set => SetValue(IsPendingProperty, value);
    }

    /// <summary>The marker color. Set by <see cref="Kind"/>; assign a brush to override it.</summary>
    public static readonly DependencyProperty MarkerBrushProperty = DependencyProperty.Register(
        nameof(MarkerBrush), typeof(Brush), typeof(TimelineItem), new PropertyMetadata(null));

    public Brush? MarkerBrush
    {
        get => (Brush?)GetValue(MarkerBrushProperty);
        set => SetValue(MarkerBrushProperty, value);
    }

    private static readonly DependencyPropertyKey SidePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Side), typeof(TimelineSide), typeof(TimelineItem), new PropertyMetadata(TimelineSide.End));

    public static readonly DependencyProperty SideProperty = SidePropertyKey.DependencyProperty;

    /// <summary>Set by the owning <see cref="Timeline"/> from its <see cref="Timeline.Layout"/>.</summary>
    public TimelineSide Side
    {
        get => (TimelineSide)GetValue(SideProperty);
        internal set => SetValue(SidePropertyKey, value);
    }

    private static readonly DependencyPropertyKey IndexPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Index), typeof(int), typeof(TimelineItem), new PropertyMetadata(1));

    public static readonly DependencyProperty IndexProperty = IndexPropertyKey.DependencyProperty;

    /// <summary>The 1-based position of the entry.</summary>
    public int Index
    {
        get => (int)GetValue(IndexProperty);
        internal set => SetValue(IndexPropertyKey, value);
    }

    private static readonly DependencyPropertyKey IsFirstPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsFirst), typeof(bool), typeof(TimelineItem), new PropertyMetadata(false));

    public static readonly DependencyProperty IsFirstProperty = IsFirstPropertyKey.DependencyProperty;

    public bool IsFirst
    {
        get => (bool)GetValue(IsFirstProperty);
        internal set => SetValue(IsFirstPropertyKey, value);
    }

    private static readonly DependencyPropertyKey IsLastPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsLast), typeof(bool), typeof(TimelineItem), new PropertyMetadata(false));

    public static readonly DependencyProperty IsLastProperty = IsLastPropertyKey.DependencyProperty;

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        internal set => SetValue(IsLastPropertyKey, value);
    }

    private static readonly DependencyPropertyKey OrientationPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Orientation), typeof(Orientation), typeof(TimelineItem), new PropertyMetadata(Orientation.Vertical));

    public static readonly DependencyProperty OrientationProperty = OrientationPropertyKey.DependencyProperty;

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        internal set => SetValue(OrientationPropertyKey, value);
    }

    private static readonly DependencyPropertyKey LayoutPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Layout), typeof(TimelineLayout), typeof(TimelineItem), new PropertyMetadata(TimelineLayout.Standard));

    public static readonly DependencyProperty LayoutProperty = LayoutPropertyKey.DependencyProperty;

    public TimelineLayout Layout
    {
        get => (TimelineLayout)GetValue(LayoutProperty);
        internal set => SetValue(LayoutPropertyKey, value);
    }
}
