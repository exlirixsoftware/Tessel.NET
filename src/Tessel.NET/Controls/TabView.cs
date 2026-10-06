using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Tessel.NET.Controls;

public sealed class TabCloseRequestedEventArgs(TabView tab, object item, TabViewItem container) : EventArgs
{
    public TabView Tab { get; } = tab;

    /// <summary>The data item (or the <see cref="TabViewItem"/> itself when tabs are declared in XAML).</summary>
    public object Item { get; } = item;

    public TabViewItem Container { get; } = container;

    /// <summary>Set to true to keep the tab open. By default the tab is removed from the items.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// A tab strip for document-style UIs: closable, reorderable (drag) tabs with an optional "add tab" button.
/// <code>&lt;tessel:TabView AddTabButtonClick="OnAddTab" TabCloseRequested="OnClose"&gt;&lt;tessel:TabViewItem Header="Doc 1" /&gt;&lt;/tessel:TabView&gt;</code>
/// </summary>
[TemplatePart(Name = "PART_HeaderScroller", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_AddButton", Type = typeof(ButtonBase))]
public class TabView : TabControl
{
    private ScrollViewer? _scroller;
    private ButtonBase? _addButton;

    // Drag-reorder state (tabs are only moved visually while dragging and committed on mouse up).
    private List<(TabViewItem Item, double Left, double Width)>? _dragLayout;
    private int _dragFrom;
    private int _dragTo;

    static TabView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TabView), new FrameworkPropertyMetadata(typeof(TabView)));
    }

    #region Dependency properties

    public static readonly DependencyProperty IsAddTabButtonVisibleProperty = DependencyProperty.Register(
        nameof(IsAddTabButtonVisible), typeof(bool), typeof(TabView), new PropertyMetadata(true));

    public bool IsAddTabButtonVisible
    {
        get => (bool)GetValue(IsAddTabButtonVisibleProperty);
        set => SetValue(IsAddTabButtonVisibleProperty, value);
    }

    public static readonly DependencyProperty CanReorderTabsProperty = DependencyProperty.Register(
        nameof(CanReorderTabs), typeof(bool), typeof(TabView), new PropertyMetadata(true));

    public bool CanReorderTabs
    {
        get => (bool)GetValue(CanReorderTabsProperty);
        set => SetValue(CanReorderTabsProperty, value);
    }

    /// <summary>Content shown at the right end of the tab strip.</summary>
    public static readonly DependencyProperty TabStripFooterProperty = DependencyProperty.Register(
        nameof(TabStripFooter), typeof(object), typeof(TabView), new PropertyMetadata(null));

    public object? TabStripFooter
    {
        get => GetValue(TabStripFooterProperty);
        set => SetValue(TabStripFooterProperty, value);
    }

    #endregion

    public event EventHandler? AddTabButtonClick;
    public event EventHandler<TabCloseRequestedEventArgs>? TabCloseRequested;

    public override void OnApplyTemplate()
    {
        if (_addButton != null) _addButton.Click -= OnAddClick;
        if (_scroller != null) _scroller.PreviewMouseWheel -= OnHeaderWheel;

        base.OnApplyTemplate();

        _scroller = GetTemplateChild("PART_HeaderScroller") as ScrollViewer;
        _addButton = GetTemplateChild("PART_AddButton") as ButtonBase;
        if (_addButton != null) _addButton.Click += OnAddClick;
        if (_scroller != null) _scroller.PreviewMouseWheel += OnHeaderWheel;
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is TabViewItem;

    protected override DependencyObject GetContainerForItemOverride() => new TabViewItem();

    private void OnAddClick(object sender, RoutedEventArgs e) => AddTabButtonClick?.Invoke(this, EventArgs.Empty);

    private void OnHeaderWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scroller == null) return;
        _scroller.ScrollToHorizontalOffset(_scroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    /// <summary>Raises <see cref="TabCloseRequested"/> and removes the tab unless the handler cancels.</summary>
    public void CloseTab(TabViewItem container)
    {
        var item = ItemContainerGenerator.ItemFromContainer(container);
        if (item == DependencyProperty.UnsetValue) item = container;

        var args = new TabCloseRequestedEventArgs(this, item, container);
        TabCloseRequested?.Invoke(this, args);
        if (args.Cancel) return;

        var index = ItemContainerGenerator.IndexFromContainer(container);
        var list = GetEditableList();
        if (list is { IsReadOnly: false, IsFixedSize: false } && index >= 0)
        {
            list.RemoveAt(index);
            if (SelectedIndex < 0 && list.Count > 0) SelectedIndex = Math.Min(index, list.Count - 1);
        }
    }

    internal IList? GetEditableList() => ItemsSource != null ? ItemsSource as IList : Items;

    #region Drag reordering

    internal void BeginDrag(TabViewItem item)
    {
        var count = Items.Count;
        _dragLayout = new List<(TabViewItem, double, double)>(count);
        for (var i = 0; i < count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is not TabViewItem container) { _dragLayout = null; return; }
            var left = container.TranslatePoint(new Point(0, 0), this).X;
            _dragLayout.Add((container, left, container.ActualWidth));
        }
        _dragFrom = _dragTo = _dragLayout.FindIndex(t => t.Item == item);
        if (_dragFrom < 0) _dragLayout = null;
    }

    internal void UpdateDrag(TabViewItem item, double deltaX)
    {
        if (_dragLayout == null) return;

        var origin = _dragLayout[_dragFrom];
        // Keep the dragged tab inside the strip.
        var minDelta = _dragLayout[0].Left - origin.Left;
        var maxDelta = _dragLayout[^1].Left + _dragLayout[^1].Width - origin.Left - origin.Width;
        deltaX = Math.Clamp(deltaX, minDelta, maxDelta);

        var center = origin.Left + origin.Width / 2 + deltaX;
        var target = _dragFrom;
        for (var i = 0; i < _dragLayout.Count; i++)
        {
            if (center >= _dragLayout[i].Left && center < _dragLayout[i].Left + _dragLayout[i].Width) { target = i; break; }
        }
        _dragTo = target;

        for (var i = 0; i < _dragLayout.Count; i++)
        {
            var tab = _dragLayout[i].Item;
            if (i == _dragFrom) SetShift(tab, deltaX);
            else if (_dragFrom < _dragTo && i > _dragFrom && i <= _dragTo) SetShift(tab, -origin.Width);
            else if (_dragFrom > _dragTo && i < _dragFrom && i >= _dragTo) SetShift(tab, origin.Width);
            else SetShift(tab, 0);
        }
        Panel.SetZIndex(item, 10);
    }

    internal void EndDrag(TabViewItem item)
    {
        if (_dragLayout == null) return;
        var layout = _dragLayout;
        var from = _dragFrom;
        var to = _dragTo;
        _dragLayout = null;

        foreach (var entry in layout)
        {
            SetShift(entry.Item, 0);
            Panel.SetZIndex(entry.Item, 0);
        }
        if (from != to) MoveItem(from, to);
    }

    private static void SetShift(TabViewItem tab, double x)
        => tab.RenderTransform = x == 0 ? Transform.Identity : new TranslateTransform(x, 0);

    private void MoveItem(int from, int to)
    {
        var list = GetEditableList();
        if (list == null || list.IsReadOnly || list.IsFixedSize) return;

        var moved = list[from];
        var move = list.GetType().GetMethod("Move", [typeof(int), typeof(int)]);
        if (move != null)
        {
            move.Invoke(list, [from, to]);
        }
        else
        {
            list.RemoveAt(from);
            list.Insert(to, moved);
        }
        SelectedItem = moved;
    }

    #endregion
}

/// <summary>A tab of a <see cref="TabView"/>.</summary>
[TemplatePart(Name = "PART_CloseButton", Type = typeof(ButtonBase))]
public class TabViewItem : TabItem
{
    private ButtonBase? _closeButton;
    private Point _dragOrigin;
    private bool _dragging;

    static TabViewItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TabViewItem), new FrameworkPropertyMetadata(typeof(TabViewItem)));
    }

    public static readonly DependencyProperty IsClosableProperty = DependencyProperty.Register(
        nameof(IsClosable), typeof(bool), typeof(TabViewItem), new PropertyMetadata(true));

    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    /// <summary>A glyph (use <c>{tessel:Glyph ...}</c>) or any element shown before the header.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(TabViewItem), new PropertyMetadata(null));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private TabView? Owner => ItemsControl.ItemsControlFromItemContainer(this) as TabView;

    public override void OnApplyTemplate()
    {
        if (_closeButton != null) _closeButton.Click -= OnCloseClick;
        base.OnApplyTemplate();
        _closeButton = GetTemplateChild("PART_CloseButton") as ButtonBase;
        if (_closeButton != null) _closeButton.Click += OnCloseClick;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (IsClosable) Owner?.CloseTab(this);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var canDrag = !e.Handled && Owner is { CanReorderTabs: true };
        base.OnMouseLeftButtonDown(e);
        if (canDrag && Owner is { } owner)
        {
            _dragOrigin = e.GetPosition(owner);
            _dragging = false;
            CaptureMouse();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed || Owner is not { } owner) return;

        var delta = e.GetPosition(owner).X - _dragOrigin.X;
        if (!_dragging)
        {
            if (Math.Abs(delta) < SystemParameters.MinimumHorizontalDragDistance) return;
            _dragging = true;
            owner.BeginDrag(this);
        }
        owner.UpdateDrag(this, delta);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        FinishDrag();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        FinishDrag();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle && IsClosable)
        {
            Owner?.CloseTab(this);
            e.Handled = true;
        }
    }

    private void FinishDrag()
    {
        var wasDragging = _dragging;
        _dragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (wasDragging) Owner?.EndDrag(this);
    }
}
