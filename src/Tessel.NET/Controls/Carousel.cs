using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Tessel.NET.Mvvm;

namespace Tessel.NET.Controls;

/// <summary>One dot of the page indicator of a <see cref="Carousel"/>.</summary>
public sealed class CarouselDot : ObservableObject
{
    private bool _isSelected;

    internal CarouselDot(int index) => Index = index;

    public int Index { get; }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// Shows one item at a time and slides to the next: a gallery of images, onboarding pages, featured cards. Arrows and dots navigate, the
/// arrow keys work when it has focus, and <see cref="AutoPlayInterval"/> turns the pages by itself (it pauses while the mouse is over the
/// carousel). The items are shown with <see cref="ItemsControl.ItemTemplate"/>, or are elements themselves.
/// </summary>
[TemplatePart(Name = "PART_Host", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Previous", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Next", Type = typeof(ButtonBase))]
public class Carousel : ItemsControl
{
    private Grid? _host;
    private ButtonBase? _previous;
    private ButtonBase? _next;
    private ContentPresenter? _current;
    private readonly DispatcherTimer _timer = new();
    private int _shownIndex = -1;

    static Carousel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Carousel), new FrameworkPropertyMetadata(typeof(Carousel)));
        FocusableProperty.OverrideMetadata(typeof(Carousel), new FrameworkPropertyMetadata(true));
    }

    public Carousel()
    {
        _timer.Tick += (_, _) => OnAutoPlay();
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnDotClick));
        Loaded += (_, _) => UpdateTimer();
        Unloaded += (_, _) => _timer.Stop();
        ((INotifyCollectionChanged)Items).CollectionChanged += OnItemsChanged;
    }

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(Carousel),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((Carousel)d).OnSelectedIndexChanged((int)e.OldValue, (int)e.NewValue), CoerceIndex));

    public static readonly DependencyProperty IsLoopingProperty = DependencyProperty.Register(
        nameof(IsLooping), typeof(bool), typeof(Carousel), new PropertyMetadata(true, (d, _) => ((Carousel)d).UpdateButtons()));

    public static readonly DependencyProperty AutoPlayIntervalProperty = DependencyProperty.Register(
        nameof(AutoPlayInterval), typeof(TimeSpan), typeof(Carousel), new PropertyMetadata(TimeSpan.Zero, (d, _) => ((Carousel)d).UpdateTimer()));

    public static readonly DependencyProperty TransitionDurationProperty = DependencyProperty.Register(
        nameof(TransitionDuration), typeof(TimeSpan), typeof(Carousel), new PropertyMetadata(TimeSpan.FromMilliseconds(320)));

    public static readonly DependencyProperty ShowArrowsProperty = DependencyProperty.Register(
        nameof(ShowArrows), typeof(bool), typeof(Carousel), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowDotsProperty = DependencyProperty.Register(
        nameof(ShowDots), typeof(bool), typeof(Carousel), new PropertyMetadata(true));

    private static readonly DependencyPropertyKey SelectedItemPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SelectedItem), typeof(object), typeof(Carousel), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty = SelectedItemPropertyKey.DependencyProperty;

    public static readonly RoutedEvent SelectedIndexChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectedIndexChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Carousel));

    /// <summary>The index of the item that is shown (-1 when there are no items).</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The item that is shown.</summary>
    public object? SelectedItem => GetValue(SelectedItemProperty);

    /// <summary>After the last item comes the first (and the other way round).</summary>
    public bool IsLooping
    {
        get => (bool)GetValue(IsLoopingProperty);
        set => SetValue(IsLoopingProperty, value);
    }

    /// <summary>The time each item stays; zero turns the pages only by hand.</summary>
    public TimeSpan AutoPlayInterval
    {
        get => (TimeSpan)GetValue(AutoPlayIntervalProperty);
        set => SetValue(AutoPlayIntervalProperty, value);
    }

    /// <summary>The length of the slide (zero switches without animation).</summary>
    public TimeSpan TransitionDuration
    {
        get => (TimeSpan)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    public bool ShowArrows
    {
        get => (bool)GetValue(ShowArrowsProperty);
        set => SetValue(ShowArrowsProperty, value);
    }

    public bool ShowDots
    {
        get => (bool)GetValue(ShowDotsProperty);
        set => SetValue(ShowDotsProperty, value);
    }

    /// <summary>The dots of the page indicator, one per item.</summary>
    public ObservableCollection<CarouselDot> Dots { get; } = [];

    public event RoutedEventHandler SelectedIndexChanged
    {
        add => AddHandler(SelectedIndexChangedEvent, value);
        remove => RemoveHandler(SelectedIndexChangedEvent, value);
    }

    /// <summary>Shows the next item (the first one after the last, when <see cref="IsLooping"/>).</summary>
    public bool Next() => Go(SelectedIndex + 1);

    public bool Previous() => Go(SelectedIndex - 1);

    private bool Go(int index)
    {
        var count = Items.Count;
        if (count < 2) return false;
        if (index >= count) index = IsLooping ? 0 : count - 1;
        if (index < 0) index = IsLooping ? count - 1 : 0;
        if (index == SelectedIndex) return false;
        SelectedIndex = index;
        return true;
    }

    private static object CoerceIndex(DependencyObject d, object value)
    {
        var count = ((Carousel)d).Items.Count;
        if (count == 0) return -1;
        return Math.Clamp((int)value, 0, count - 1);
    }

    #region Template

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_previous != null) _previous.Click -= OnPreviousClick;
        if (_next != null) _next.Click -= OnNextClick;

        _host = GetTemplateChild("PART_Host") as Grid;
        _previous = GetTemplateChild("PART_Previous") as ButtonBase;
        _next = GetTemplateChild("PART_Next") as ButtonBase;
        if (_previous != null) _previous.Click += OnPreviousClick;
        if (_next != null) _next.Click += OnNextClick;

        _host?.Children.Clear();
        _current = null;
        _shownIndex = -1;
        KeepIndexValid();
        Show(SelectedIndex, 0);
        UpdateDots();
        UpdateButtons();
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        Previous();
        e.Handled = true;
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        Next();
        e.Handled = true;
    }

    private void OnDotClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: CarouselDot dot })
        {
            SelectedIndex = dot.Index;
            e.Handled = true;
        }
    }

    #endregion

    #region Selection

    /// <summary>Limits the index to the items and stores the limited value, so a later change of the items does not bring back an old index.</summary>
    private void KeepIndexValid()
    {
        CoerceValue(SelectedIndexProperty);
        SetCurrentValue(SelectedIndexProperty, SelectedIndex);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        KeepIndexValid();
        UpdateDots();
        // the shown item may have been removed or replaced: show whatever is selected now
        _shownIndex = -2;
        Show(SelectedIndex, 0);
        UpdateButtons();
        UpdateTimer();
    }

    private void OnSelectedIndexChanged(int oldIndex, int newIndex)
    {
        Show(newIndex, newIndex > oldIndex ? 1 : -1);
        UpdateDots();
        UpdateButtons();
        RaiseEvent(new RoutedEventArgs(SelectedIndexChangedEvent));
    }

    private void UpdateDots()
    {
        while (Dots.Count > Items.Count) Dots.RemoveAt(Dots.Count - 1);
        while (Dots.Count < Items.Count) Dots.Add(new CarouselDot(Dots.Count));
        for (var i = 0; i < Dots.Count; i++) Dots[i].IsSelected = i == SelectedIndex;
    }

    private void UpdateButtons()
    {
        var count = Items.Count;
        var enabled = count > 1;
        if (_previous != null) _previous.IsEnabled = enabled && (IsLooping || SelectedIndex > 0);
        if (_next != null) _next.IsEnabled = enabled && (IsLooping || SelectedIndex < count - 1);
    }

    /// <summary>Puts the item at <paramref name="index"/> on screen, sliding in from the side given by <paramref name="direction"/> (0 = no animation).</summary>
    private void Show(int index, int direction)
    {
        var item = index >= 0 && index < Items.Count ? Items[index] : null;
        SetValue(SelectedItemPropertyKey, item);
        if (_host == null || index == _shownIndex) return;
        _shownIndex = index;

        // finish a slide that is still running, so an element is never in two presenters
        for (var i = _host.Children.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_host.Children[i], _current))
            {
                var stale = (ContentPresenter)_host.Children[i];
                stale.BeginAnimation(OpacityProperty, null);
                stale.Content = null;
                _host.Children.RemoveAt(i);
            }
        }

        var old = _current;
        _current = null;
        if (item != null)
        {
            if (old != null && ReferenceEquals(old.Content, item))
            {
                _current = old;
                return;
            }
            _current = new ContentPresenter
            {
                Content = item,
                ContentTemplate = ItemTemplate,
                ContentTemplateSelector = ItemTemplateSelector,
                RenderTransform = new TranslateTransform(),
            };
            _host.Children.Add(_current);
        }

        var duration = TransitionDuration;
        if (old == null || direction == 0 || duration <= TimeSpan.Zero || !IsLoaded)
        {
            if (old != null)
            {
                old.Content = null;
                _host.Children.Remove(old);
            }
            return;
        }

        var distance = Math.Max(24, _host.ActualWidth * 0.12);
        Slide(old, 0, -direction * distance, 1, 0, () =>
        {
            old.Content = null;
            _host.Children.Remove(old);
        });
        if (_current != null) Slide(_current, direction * distance, 0, 0, 1, null);
    }

    private void Slide(ContentPresenter presenter, double fromX, double toX, double fromOpacity, double toOpacity, Action? completed)
    {
        var duration = new Duration(TransitionDuration);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        if (presenter.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            presenter.RenderTransform = translate;
        }
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromX, toX, duration) { EasingFunction = easing });
        var fade = new DoubleAnimation(fromOpacity, toOpacity, duration) { EasingFunction = easing };
        if (completed != null) fade.Completed += (_, _) => completed();
        presenter.BeginAnimation(OpacityProperty, fade);
    }

    #endregion

    #region Auto play and input

    private void UpdateTimer()
    {
        _timer.Stop();
        if (AutoPlayInterval <= TimeSpan.Zero || Items.Count < 2 || !IsLoaded) return;
        _timer.Interval = AutoPlayInterval;
        _timer.Start();
    }

    private void OnAutoPlay()
    {
        if (IsMouseOver || IsKeyboardFocusWithin) return;
        if (!Next()) _timer.Stop();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Right && Next()) e.Handled = true;
        else if (e.Key == Key.Left && Previous()) e.Handled = true;
        else if (e.Key == Key.Home) { SelectedIndex = 0; e.Handled = true; }
        else if (e.Key == Key.End) { SelectedIndex = Items.Count - 1; e.Handled = true; }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }

    #endregion
}
