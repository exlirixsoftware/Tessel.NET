using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace Tessel.NET.Controls;

public sealed class BreadcrumbBarItemClickedEventArgs(int index, object item) : EventArgs
{
    /// <summary>Zero-based position of the clicked crumb.</summary>
    public int Index { get; } = index;

    public object Item { get; } = item;
}

/// <summary>
/// Shows the path to the current location. The last item is the current page and is not clickable.
/// <code>&lt;tessel:BreadcrumbBar ItemsSource="{Binding Path}" ItemClicked="OnCrumbClicked" /&gt;</code>
/// </summary>
public class BreadcrumbBar : ItemsControl
{
    static BreadcrumbBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BreadcrumbBar), new FrameworkPropertyMetadata(typeof(BreadcrumbBar)));
        FocusableProperty.OverrideMetadata(typeof(BreadcrumbBar), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(BreadcrumbBar), new FrameworkPropertyMetadata(false));
    }

    public event EventHandler<BreadcrumbBarItemClickedEventArgs>? ItemClicked;

    protected override bool IsItemItsOwnContainerOverride(object item) => item is BreadcrumbBarItem;

    protected override DependencyObject GetContainerForItemOverride() => new BreadcrumbBarItem();

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

    private void ScheduleUpdate() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateLastItem);

    private void UpdateLastItem()
    {
        var count = Items.Count;
        for (var i = 0; i < count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is BreadcrumbBarItem crumb) crumb.IsLast = i == count - 1;
        }
    }

    internal void RaiseItemClicked(BreadcrumbBarItem container)
    {
        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0) return;
        var item = ItemContainerGenerator.ItemFromContainer(container);
        if (item == DependencyProperty.UnsetValue) item = container;
        ItemClicked?.Invoke(this, new BreadcrumbBarItemClickedEventArgs(index, item));
    }
}

/// <summary>One crumb of a <see cref="BreadcrumbBar"/>.</summary>
[TemplatePart(Name = "PART_Button", Type = typeof(ButtonBase))]
public class BreadcrumbBarItem : ContentControl
{
    private ButtonBase? _button;

    static BreadcrumbBarItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BreadcrumbBarItem), new FrameworkPropertyMetadata(typeof(BreadcrumbBarItem)));
        FocusableProperty.OverrideMetadata(typeof(BreadcrumbBarItem), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(BreadcrumbBarItem), new FrameworkPropertyMetadata(false));
    }

    /// <summary>True for the last crumb (the current location).</summary>
    public static readonly DependencyProperty IsLastProperty = DependencyProperty.Register(
        nameof(IsLast), typeof(bool), typeof(BreadcrumbBarItem), new PropertyMetadata(false));

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        internal set => SetValue(IsLastProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (_button != null) _button.Click -= OnButtonClick;
        base.OnApplyTemplate();
        _button = GetTemplateChild("PART_Button") as ButtonBase;
        if (_button != null) _button.Click += OnButtonClick;
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (!IsLast) (ItemsControl.ItemsControlFromItemContainer(this) as BreadcrumbBar)?.RaiseItemClicked(this);
    }
}
