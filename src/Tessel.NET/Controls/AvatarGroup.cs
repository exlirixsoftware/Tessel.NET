using System;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tessel.NET.Controls;

/// <summary>
/// A row of overlapping <see cref="Avatar"/>s for the members of a team, the viewers of a document or the people in a thread. Beyond
/// <see cref="MaxVisible"/> the rest are summarized as "+3". The items are names (strings) or any objects, read with
/// <see cref="DisplayMemberPath"/>, <see cref="ImageMemberPath"/> and <see cref="PresenceMemberPath"/>.
/// </summary>
[TemplatePart(Name = "PART_Panel", Type = typeof(StackPanel))]
public class AvatarGroup : Control
{
    private StackPanel? _panel;
    private INotifyCollectionChanged? _observed;

    static AvatarGroup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(typeof(AvatarGroup)));
        FocusableProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(AvatarGroup), new PropertyMetadata(null, (d, e) => ((AvatarGroup)d).OnItemsSourceChanged((IEnumerable?)e.NewValue)));

    public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
        nameof(DisplayMemberPath), typeof(string), typeof(AvatarGroup), new PropertyMetadata(null, (d, _) => ((AvatarGroup)d).Rebuild()));

    public static readonly DependencyProperty ImageMemberPathProperty = DependencyProperty.Register(
        nameof(ImageMemberPath), typeof(string), typeof(AvatarGroup), new PropertyMetadata(null, (d, _) => ((AvatarGroup)d).Rebuild()));

    public static readonly DependencyProperty PresenceMemberPathProperty = DependencyProperty.Register(
        nameof(PresenceMemberPath), typeof(string), typeof(AvatarGroup), new PropertyMetadata(null, (d, _) => ((AvatarGroup)d).Rebuild()));

    public static readonly DependencyProperty MaxVisibleProperty = DependencyProperty.Register(
        nameof(MaxVisible), typeof(int), typeof(AvatarGroup), new PropertyMetadata(4, (d, _) => ((AvatarGroup)d).Rebuild(), (_, v) => Math.Max(1, (int)v)));

    public static readonly DependencyProperty AvatarSizeProperty = DependencyProperty.Register(
        nameof(AvatarSize), typeof(double), typeof(AvatarGroup), new PropertyMetadata(36d, (d, _) => ((AvatarGroup)d).Rebuild(), (_, v) => Math.Max(12, (double)v)));

    public static readonly DependencyProperty OverlapProperty = DependencyProperty.Register(
        nameof(Overlap), typeof(double), typeof(AvatarGroup), new PropertyMetadata(10d, (d, _) => ((AvatarGroup)d).Rebuild(), (_, v) => Math.Max(0, (double)v)));

    /// <summary>The people: names, or objects described by the member paths.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>The property that holds the name (not needed for strings).</summary>
    public string? DisplayMemberPath
    {
        get => (string?)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    /// <summary>The property that holds an <see cref="ImageSource"/> (optional).</summary>
    public string? ImageMemberPath
    {
        get => (string?)GetValue(ImageMemberPathProperty);
        set => SetValue(ImageMemberPathProperty, value);
    }

    /// <summary>The property that holds an <see cref="AvatarPresence"/> (optional).</summary>
    public string? PresenceMemberPath
    {
        get => (string?)GetValue(PresenceMemberPathProperty);
        set => SetValue(PresenceMemberPathProperty, value);
    }

    /// <summary>How many avatars are shown before the "+N" circle.</summary>
    public int MaxVisible
    {
        get => (int)GetValue(MaxVisibleProperty);
        set => SetValue(MaxVisibleProperty, value);
    }

    public double AvatarSize
    {
        get => (double)GetValue(AvatarSizeProperty);
        set => SetValue(AvatarSizeProperty, value);
    }

    /// <summary>How far each avatar covers the one before it.</summary>
    public double Overlap
    {
        get => (double)GetValue(OverlapProperty);
        set => SetValue(OverlapProperty, value);
    }

    /// <summary>The number of people that did not fit and are summarized in the "+N" circle.</summary>
    public int HiddenCount { get; private set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _panel = GetTemplateChild("PART_Panel") as StackPanel;
        Rebuild();
    }

    private void OnItemsSourceChanged(IEnumerable? items)
    {
        if (_observed != null) _observed.CollectionChanged -= OnItemsChanged;
        _observed = items as INotifyCollectionChanged;
        if (_observed != null) _observed.CollectionChanged += OnItemsChanged;
        Rebuild();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private static object? Read(object item, string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return item.GetType().GetProperty(path)?.GetValue(item);
    }

    private void Rebuild()
    {
        if (_panel == null) return;
        _panel.Children.Clear();

        var items = new System.Collections.Generic.List<object>();
        if (ItemsSource != null)
        {
            foreach (var item in ItemsSource)
            {
                if (item != null) items.Add(item);
            }
        }

        var shown = items.Count > MaxVisible ? MaxVisible : items.Count;
        // one slot of the limit is the "+N" circle, so the total never exceeds MaxVisible + 1
        HiddenCount = items.Count - shown;
        var size = AvatarSize;

        for (var i = 0; i < shown; i++)
        {
            var item = items[i];
            var name = (item as string) ?? Read(item, DisplayMemberPath)?.ToString() ?? item.ToString();
            var avatar = new Avatar
            {
                DisplayName = name,
                Width = size,
                Height = size,
                ImageSource = Read(item, ImageMemberPath) as ImageSource,
                ToolTip = name,
            };
            if (Read(item, PresenceMemberPath) is AvatarPresence presence) avatar.Presence = presence;
            _panel.Children.Add(Ring(avatar, size, i));
        }

        if (HiddenCount > 0)
        {
            var more = new TextBlock
            {
                Text = "+" + HiddenCount,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = Math.Max(9, size * 0.34),
                FontWeight = FontWeights.SemiBold,
            };
            more.SetResourceReference(TextBlock.ForegroundProperty, "Tessel.TextBrush");
            var circle = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Child = more,
            };
            circle.SetResourceReference(Border.BackgroundProperty, "Tessel.SurfaceAltBrush");
            _panel.Children.Add(Ring(circle, size, shown));
        }
    }

    /// <summary>Wraps a circle in a ring of the page color, so neighbors are separated where they overlap.</summary>
    private FrameworkElement Ring(FrameworkElement content, double size, int index)
    {
        const double ringWidth = 2;
        var ring = new Border
        {
            Width = size + 2 * ringWidth,
            Height = size + 2 * ringWidth,
            CornerRadius = new CornerRadius(size / 2 + ringWidth),
            BorderThickness = new Thickness(ringWidth),
            Margin = new Thickness(index == 0 ? 0 : -Overlap, 0, 0, 0),
            Child = content,
        };
        ring.SetResourceReference(Border.BorderBrushProperty, "Tessel.SurfaceBrush");
        ring.SetResourceReference(Border.BackgroundProperty, "Tessel.SurfaceBrush");
        return ring;
    }
}
