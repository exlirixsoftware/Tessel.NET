using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Tessel.NET.Controls;

/// <summary>An item of a <see cref="NavigationView"/>.</summary>
public class NavigationViewItem : ListBoxItem
{
    static NavigationViewItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NavigationViewItem), new FrameworkPropertyMetadata(typeof(NavigationViewItem)));
    }

    /// <summary>A glyph string (use <c>{tessel:Glyph ...}</c>) or any element.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(NavigationViewItem), new PropertyMetadata(null));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>The page type created when this item is selected.</summary>
    public static readonly DependencyProperty TargetPageTypeProperty = DependencyProperty.Register(
        nameof(TargetPageType), typeof(Type), typeof(NavigationViewItem), new PropertyMetadata(null));

    public Type? TargetPageType
    {
        get => (Type?)GetValue(TargetPageTypeProperty);
        set => SetValue(TargetPageTypeProperty, value);
    }
}

/// <summary>A non-selectable section header inside a <see cref="NavigationView"/>.</summary>
public class NavigationViewItemHeader : ListBoxItem
{
    static NavigationViewItemHeader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NavigationViewItemHeader), new FrameworkPropertyMetadata(typeof(NavigationViewItemHeader)));
        FocusableProperty.OverrideMetadata(typeof(NavigationViewItemHeader), new FrameworkPropertyMetadata(false));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(NavigationViewItemHeader), new UIPropertyMetadata(false));
    }
}

public sealed class NavigationViewSelectionChangedEventArgs(object? oldItem, object? newItem) : EventArgs
{
    public object? OldItem { get; } = oldItem;
    public object? NewItem { get; } = newItem;
}

/// <summary>
/// App navigation: a collapsible side pane with menu and footer items, and a content area.
/// Items with a <see cref="NavigationViewItem.TargetPageType"/> create (and cache) that page when selected.
/// </summary>
[TemplatePart(Name = "PART_MenuList", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_FooterList", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_PaneToggleButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_ContentPresenter", Type = typeof(FrameworkElement))]
public class NavigationView : ContentControl
{
    private readonly Dictionary<Type, object> _pageCache = new();
    private ListBox? _menuList;
    private ListBox? _footerList;
    private ButtonBase? _toggleButton;
    private FrameworkElement? _contentPresenter;
    private bool _syncing;

    static NavigationView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NavigationView), new FrameworkPropertyMetadata(typeof(NavigationView)));
    }

    public NavigationView()
    {
        SetValue(MenuItemsPropertyKey, new ObservableCollection<object>());
        SetValue(FooterMenuItemsPropertyKey, new ObservableCollection<object>());
    }

    #region Dependency properties

    private static readonly DependencyPropertyKey MenuItemsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(MenuItems), typeof(ObservableCollection<object>), typeof(NavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty MenuItemsProperty = MenuItemsPropertyKey.DependencyProperty;

    public ObservableCollection<object> MenuItems => (ObservableCollection<object>)GetValue(MenuItemsProperty);

    private static readonly DependencyPropertyKey FooterMenuItemsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(FooterMenuItems), typeof(ObservableCollection<object>), typeof(NavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty FooterMenuItemsProperty = FooterMenuItemsPropertyKey.DependencyProperty;

    public ObservableCollection<object> FooterMenuItems => (ObservableCollection<object>)GetValue(FooterMenuItemsProperty);

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(NavigationView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly DependencyProperty IsPaneOpenProperty = DependencyProperty.Register(
        nameof(IsPaneOpen), typeof(bool), typeof(NavigationView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    public static readonly DependencyProperty OpenPaneLengthProperty = DependencyProperty.Register(
        nameof(OpenPaneLength), typeof(double), typeof(NavigationView), new PropertyMetadata(260d));

    public double OpenPaneLength
    {
        get => (double)GetValue(OpenPaneLengthProperty);
        set => SetValue(OpenPaneLengthProperty, value);
    }

    public static readonly DependencyProperty CompactPaneLengthProperty = DependencyProperty.Register(
        nameof(CompactPaneLength), typeof(double), typeof(NavigationView), new PropertyMetadata(56d));

    public double CompactPaneLength
    {
        get => (double)GetValue(CompactPaneLengthProperty);
        set => SetValue(CompactPaneLengthProperty, value);
    }

    public static readonly DependencyProperty PaneTitleProperty = DependencyProperty.Register(
        nameof(PaneTitle), typeof(object), typeof(NavigationView), new PropertyMetadata(null));

    public object? PaneTitle
    {
        get => GetValue(PaneTitleProperty);
        set => SetValue(PaneTitleProperty, value);
    }

    /// <summary>Content shown under the pane title, e.g. a search box.</summary>
    public static readonly DependencyProperty PaneHeaderProperty = DependencyProperty.Register(
        nameof(PaneHeader), typeof(object), typeof(NavigationView), new PropertyMetadata(null));

    public object? PaneHeader
    {
        get => GetValue(PaneHeaderProperty);
        set => SetValue(PaneHeaderProperty, value);
    }

    public static readonly DependencyProperty IsPaneToggleButtonVisibleProperty = DependencyProperty.Register(
        nameof(IsPaneToggleButtonVisible), typeof(bool), typeof(NavigationView), new PropertyMetadata(true));

    public bool IsPaneToggleButtonVisible
    {
        get => (bool)GetValue(IsPaneToggleButtonVisibleProperty);
        set => SetValue(IsPaneToggleButtonVisibleProperty, value);
    }

    /// <summary>When true (default) pages created for <see cref="NavigationViewItem.TargetPageType"/> are reused.</summary>
    public static readonly DependencyProperty IsPageCacheEnabledProperty = DependencyProperty.Register(
        nameof(IsPageCacheEnabled), typeof(bool), typeof(NavigationView), new PropertyMetadata(true));

    public bool IsPageCacheEnabled
    {
        get => (bool)GetValue(IsPageCacheEnabledProperty);
        set => SetValue(IsPageCacheEnabledProperty, value);
    }

    #endregion

    /// <summary>Creates pages; defaults to <see cref="Activator.CreateInstance(Type)"/>. Plug in your DI container here.</summary>
    public Func<Type, object>? PageFactory { get; set; }

    public event EventHandler<NavigationViewSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Selects the item targeting <paramref name="pageType"/>, or shows the page directly if no item matches.</summary>
    public void Navigate(Type pageType)
    {
        var item = MenuItems.Concat(FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.TargetPageType == pageType);

        if (item != null)
        {
            SelectedItem = item;
        }
        else
        {
            Content = GetPage(pageType);
        }
    }

    public override void OnApplyTemplate()
    {
        if (_menuList != null) _menuList.SelectionChanged -= OnListSelectionChanged;
        if (_footerList != null) _footerList.SelectionChanged -= OnListSelectionChanged;
        if (_toggleButton != null) _toggleButton.Click -= OnToggleClick;

        base.OnApplyTemplate();

        _menuList = GetTemplateChild("PART_MenuList") as ListBox;
        _footerList = GetTemplateChild("PART_FooterList") as ListBox;
        _toggleButton = GetTemplateChild("PART_PaneToggleButton") as ButtonBase;
        _contentPresenter = GetTemplateChild("PART_ContentPresenter") as FrameworkElement;

        if (_menuList != null) _menuList.SelectionChanged += OnListSelectionChanged;
        if (_footerList != null) _footerList.SelectionChanged += OnListSelectionChanged;
        if (_toggleButton != null) _toggleButton.Click += OnToggleClick;

        SyncListSelection();
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        AnimateContent();
    }

    private void OnToggleClick(object sender, RoutedEventArgs e) => SetCurrentValue(IsPaneOpenProperty, !IsPaneOpen);

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || sender is not ListBox { SelectedItem: { } selected }) return;
        SetCurrentValue(SelectedItemProperty, selected);
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (NavigationView)d;
        view.SyncListSelection();

        if (e.NewValue is NavigationViewItem { TargetPageType: { } pageType })
        {
            view.Content = view.GetPage(pageType);
        }

        view.SelectionChanged?.Invoke(view, new NavigationViewSelectionChangedEventArgs(e.OldValue, e.NewValue));
    }

    private void SyncListSelection()
    {
        _syncing = true;
        try
        {
            if (_menuList != null) _menuList.SelectedItem = MenuItems.Contains(SelectedItem!) ? SelectedItem : null;
            if (_footerList != null) _footerList.SelectedItem = FooterMenuItems.Contains(SelectedItem!) ? SelectedItem : null;
        }
        finally
        {
            _syncing = false;
        }
    }

    private object GetPage(Type pageType)
    {
        if (IsPageCacheEnabled && _pageCache.TryGetValue(pageType, out var cached)) return cached;

        var page = PageFactory?.Invoke(pageType) ?? Activator.CreateInstance(pageType)
            ?? throw new InvalidOperationException($"Could not create page {pageType}.");

        if (IsPageCacheEnabled) _pageCache[pageType] = page;
        return page;
    }

    private void AnimateContent()
    {
        if (_contentPresenter == null || !IsLoaded) return;

        var duration = new Duration(TimeSpan.FromMilliseconds(220));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var translate = new TranslateTransform();
        _contentPresenter.RenderTransform = translate;
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, duration) { EasingFunction = ease });
        _contentPresenter.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
    }
}
