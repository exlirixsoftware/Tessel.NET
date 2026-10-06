using System.Windows;
using System.Windows.Controls;

namespace Tessel.NET.Controls;

/// <summary>
/// A row of mutually exclusive options ("Day | Week | Month"). Add <see cref="SegmentedItem"/>s or bind
/// <see cref="ItemsControl.ItemsSource"/>; read the selection through <c>SelectedItem</c> / <c>SelectedIndex</c>.
/// </summary>
public class SegmentedControl : ListBox
{
    static SegmentedControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SegmentedControl), new FrameworkPropertyMetadata(typeof(SegmentedControl)));
    }

    public SegmentedControl()
    {
        SelectionMode = SelectionMode.Single;
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is SegmentedItem;

    protected override DependencyObject GetContainerForItemOverride() => new SegmentedItem();
}

/// <summary>An option of a <see cref="SegmentedControl"/>.</summary>
public class SegmentedItem : ListBoxItem
{
    static SegmentedItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SegmentedItem), new FrameworkPropertyMetadata(typeof(SegmentedItem)));
    }

    /// <summary>A glyph (use <c>{tessel:Glyph ...}</c>) or any element shown before the content.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(SegmentedItem), new PropertyMetadata(null));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
