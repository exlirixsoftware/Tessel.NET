using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Tessel.NET.Controls;

/// <summary>One entry of the page strip of a <see cref="Pagination"/> control (a page number or an ellipsis).</summary>
public sealed class PaginationPage
{
    public PaginationPage(int number, bool isCurrent)
    {
        Number = number;
        IsCurrent = isCurrent;
        Text = number.ToString();
    }

    private PaginationPage() => Text = "…";

    internal static PaginationPage Ellipsis() => new() { IsEllipsis = true };

    /// <summary>The 1-based page number (0 for an ellipsis).</summary>
    public int Number { get; }

    public bool IsCurrent { get; }

    public bool IsEllipsis { get; private init; }

    public string Text { get; }
}

/// <summary>
/// Page navigation for lists and tables: previous/next buttons and a compact strip of page numbers.
/// Bind <see cref="CurrentPage"/> (1-based) and set <see cref="PageCount"/>.
/// </summary>
[TemplatePart(Name = "PART_PreviousButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_NextButton", Type = typeof(ButtonBase))]
public class Pagination : Control
{
    private ButtonBase? _previous;
    private ButtonBase? _next;

    static Pagination()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Pagination), new FrameworkPropertyMetadata(typeof(Pagination)));
        FocusableProperty.OverrideMetadata(typeof(Pagination), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Pagination), new FrameworkPropertyMetadata(false));
    }

    public Pagination()
    {
        Pages = [];
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnPageButtonClick));
        Rebuild();
    }

    #region Dependency properties

    public static readonly DependencyProperty PageCountProperty = DependencyProperty.Register(
        nameof(PageCount), typeof(int), typeof(Pagination),
        new PropertyMetadata(1, OnPagingChanged, (_, v) => Math.Max(1, (int)v)));

    public int PageCount
    {
        get => (int)GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(
        nameof(CurrentPage), typeof(int), typeof(Pagination),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentPageChanged, CoerceCurrentPage));

    /// <summary>The selected page, from 1 to <see cref="PageCount"/>.</summary>
    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    /// <summary>How many entries (numbers and ellipses) the strip shows at most (minimum 5).</summary>
    public static readonly DependencyProperty MaxVisiblePagesProperty = DependencyProperty.Register(
        nameof(MaxVisiblePages), typeof(int), typeof(Pagination), new PropertyMetadata(7, OnPagingChanged));

    public int MaxVisiblePages
    {
        get => (int)GetValue(MaxVisiblePagesProperty);
        set => SetValue(MaxVisiblePagesProperty, value);
    }

    private static readonly DependencyPropertyKey PagesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Pages), typeof(ObservableCollection<PaginationPage>), typeof(Pagination), new PropertyMetadata(null));

    public static readonly DependencyProperty PagesProperty = PagesPropertyKey.DependencyProperty;

    /// <summary>The entries currently shown in the strip.</summary>
    public ObservableCollection<PaginationPage> Pages
    {
        get => (ObservableCollection<PaginationPage>)GetValue(PagesProperty);
        private set => SetValue(PagesPropertyKey, value);
    }

    public static readonly DependencyProperty CanGoPreviousProperty = DependencyProperty.Register(
        nameof(CanGoPrevious), typeof(bool), typeof(Pagination), new PropertyMetadata(false));

    public bool CanGoPrevious
    {
        get => (bool)GetValue(CanGoPreviousProperty);
        private set => SetValue(CanGoPreviousProperty, value);
    }

    public static readonly DependencyProperty CanGoNextProperty = DependencyProperty.Register(
        nameof(CanGoNext), typeof(bool), typeof(Pagination), new PropertyMetadata(false));

    public bool CanGoNext
    {
        get => (bool)GetValue(CanGoNextProperty);
        private set => SetValue(CanGoNextProperty, value);
    }

    public static readonly RoutedEvent CurrentPageChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(CurrentPageChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<int>), typeof(Pagination));

    public event RoutedPropertyChangedEventHandler<int> CurrentPageChanged
    {
        add => AddHandler(CurrentPageChangedEvent, value);
        remove => RemoveHandler(CurrentPageChangedEvent, value);
    }

    #endregion

    public override void OnApplyTemplate()
    {
        if (_previous != null) _previous.Click -= OnPreviousClick;
        if (_next != null) _next.Click -= OnNextClick;

        base.OnApplyTemplate();

        _previous = GetTemplateChild("PART_PreviousButton") as ButtonBase;
        _next = GetTemplateChild("PART_NextButton") as ButtonBase;
        if (_previous != null) _previous.Click += OnPreviousClick;
        if (_next != null) _next.Click += OnNextClick;
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        CurrentPage--;
        e.Handled = true;
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        CurrentPage++;
        e.Handled = true;
    }

    private void OnPageButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: PaginationPage { IsEllipsis: false } page })
        {
            CurrentPage = page.Number;
            e.Handled = true;
        }
    }

    private static object CoerceCurrentPage(DependencyObject d, object value)
        => Math.Clamp((int)value, 1, ((Pagination)d).PageCount);

    private static void OnPagingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pagination = (Pagination)d;
        pagination.CoerceValue(CurrentPageProperty);
        pagination.Rebuild();
    }

    private static void OnCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pagination = (Pagination)d;
        pagination.Rebuild();
        pagination.RaiseEvent(new RoutedPropertyChangedEventArgs<int>((int)e.OldValue, (int)e.NewValue, CurrentPageChangedEvent));
    }

    private void Rebuild()
    {
        var count = PageCount;
        var current = CurrentPage;
        CanGoPrevious = current > 1;
        CanGoNext = current < count;

        Pages.Clear();
        var max = Math.Max(5, MaxVisiblePages);
        if (count <= max)
        {
            for (var i = 1; i <= count; i++) Pages.Add(new PaginationPage(i, i == current));
            return;
        }

        // First and last page are always shown; a window of pages around the current one sits between them.
        var slots = max - 2;
        var start = Math.Clamp(current - slots / 2, 2, count - slots);
        var end = start + slots - 1;

        Pages.Add(new PaginationPage(1, current == 1));
        for (var i = start; i <= end; i++)
        {
            var isEllipsis = (i == start && start > 2) || (i == end && end < count - 1);
            Pages.Add(isEllipsis ? PaginationPage.Ellipsis() : new PaginationPage(i, i == current));
        }
        Pages.Add(new PaginationPage(count, current == count));
    }
}
