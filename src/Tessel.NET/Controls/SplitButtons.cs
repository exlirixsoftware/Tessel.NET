using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Tessel.NET.Controls;

/// <summary>Opens a <see cref="ContextMenu"/> below an element; shared by the drop-down button controls.</summary>
internal static class DropDownHelper
{
    // A click on the button while its menu is open first light-dismisses the menu (mouse down) and then raises Click.
    // Without this the menu would close and immediately re-open.
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);
    private static readonly ConditionalWeakTable<ContextMenu, StrongBox<long>> LastClosed = new();

    public static void Toggle(FrameworkElement owner, ContextMenu? menu, bool fromClick = true)
    {
        if (menu == null) return;
        if (menu.IsOpen)
        {
            menu.IsOpen = false;
            return;
        }
        if (fromClick && LastClosed.TryGetValue(menu, out var closedAt)
            && Environment.TickCount64 - closedAt.Value < ReopenGuard.TotalMilliseconds)
        {
            return;
        }

        // The menu is not part of the owner's tree, so make sure it picks up the Tessel style.
        if (menu.Style == null && owner.TryFindResource(typeof(ContextMenu)) is Style style) menu.Style = style;
        menu.PlacementTarget = owner;
        menu.Placement = PlacementMode.Bottom;
        menu.MinWidth = owner.ActualWidth;
        menu.IsOpen = true;
    }

    public static void Hook(ContextMenu? oldMenu, ContextMenu? newMenu, RoutedEventHandler opened, RoutedEventHandler closed)
    {
        if (oldMenu != null)
        {
            oldMenu.Opened -= opened;
            oldMenu.Closed -= closed;
            oldMenu.Closed -= RecordClose;
        }
        if (newMenu != null)
        {
            newMenu.Opened += opened;
            newMenu.Closed += closed;
            newMenu.Closed += RecordClose;
        }
    }

    private static void RecordClose(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)sender;
        LastClosed.Remove(menu);
        LastClosed.Add(menu, new StrongBox<long>(Environment.TickCount64));
    }
}

/// <summary>A button with a chevron that opens a menu when clicked.</summary>
public class DropDownButton : Button
{
    static DropDownButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DropDownButton), new FrameworkPropertyMetadata(typeof(DropDownButton)));
    }

    public static readonly DependencyProperty DropDownMenuProperty = DependencyProperty.Register(
        nameof(DropDownMenu), typeof(ContextMenu), typeof(DropDownButton),
        new PropertyMetadata(null, (d, e) =>
        {
            var button = (DropDownButton)d;
            DropDownHelper.Hook((ContextMenu?)e.OldValue, (ContextMenu?)e.NewValue, button.OnMenuOpened, button.OnMenuClosed);
        }));

    /// <summary>The menu shown below the button.</summary>
    public ContextMenu? DropDownMenu
    {
        get => (ContextMenu?)GetValue(DropDownMenuProperty);
        set => SetValue(DropDownMenuProperty, value);
    }

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen), typeof(bool), typeof(DropDownButton), new PropertyMetadata(false));

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set
        {
            if (DropDownMenu is { } menu && menu.IsOpen != value) DropDownHelper.Toggle(this, menu, fromClick: false);
        }
    }

    protected override void OnClick()
    {
        base.OnClick();
        DropDownHelper.Toggle(this, DropDownMenu);
    }

    private void OnMenuOpened(object sender, RoutedEventArgs e) => SetCurrentValue(IsDropDownOpenProperty, true);

    private void OnMenuClosed(object sender, RoutedEventArgs e) => SetCurrentValue(IsDropDownOpenProperty, false);
}

/// <summary>A button with two parts: the main action (<see cref="ButtonBase.Click"/>) and a chevron that opens a menu.</summary>
[TemplatePart(Name = "PART_ArrowButton", Type = typeof(ButtonBase))]
public class SplitButton : Button
{
    private ButtonBase? _arrow;

    static SplitButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton), new FrameworkPropertyMetadata(typeof(SplitButton)));
    }

    public static readonly DependencyProperty DropDownMenuProperty = DependencyProperty.Register(
        nameof(DropDownMenu), typeof(ContextMenu), typeof(SplitButton),
        new PropertyMetadata(null, (d, e) =>
        {
            var button = (SplitButton)d;
            DropDownHelper.Hook((ContextMenu?)e.OldValue, (ContextMenu?)e.NewValue, button.OnMenuOpened, button.OnMenuClosed);
        }));

    /// <summary>The menu shown below the button when the chevron is clicked.</summary>
    public ContextMenu? DropDownMenu
    {
        get => (ContextMenu?)GetValue(DropDownMenuProperty);
        set => SetValue(DropDownMenuProperty, value);
    }

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen), typeof(bool), typeof(SplitButton), new PropertyMetadata(false));

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set
        {
            if (DropDownMenu is { } menu && menu.IsOpen != value) DropDownHelper.Toggle(this, menu, fromClick: false);
        }
    }

    public override void OnApplyTemplate()
    {
        if (_arrow != null) _arrow.Click -= OnArrowClick;
        base.OnApplyTemplate();
        _arrow = GetTemplateChild("PART_ArrowButton") as ButtonBase;
        if (_arrow != null) _arrow.Click += OnArrowClick;
    }

    private void OnArrowClick(object sender, RoutedEventArgs e)
    {
        DropDownHelper.Toggle(this, DropDownMenu);
        e.Handled = true;
    }

    private void OnMenuOpened(object sender, RoutedEventArgs e) => SetCurrentValue(IsDropDownOpenProperty, true);

    private void OnMenuClosed(object sender, RoutedEventArgs e) => SetCurrentValue(IsDropDownOpenProperty, false);
}

/// <summary>A toggle button (<see cref="ToggleButton.IsChecked"/>) with a chevron that opens a menu.</summary>
[TemplatePart(Name = "PART_ArrowButton", Type = typeof(ButtonBase))]
public class ToggleSplitButton : ToggleButton
{
    private ButtonBase? _arrow;

    static ToggleSplitButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSplitButton), new FrameworkPropertyMetadata(typeof(ToggleSplitButton)));
    }

    public static readonly DependencyProperty DropDownMenuProperty = DependencyProperty.Register(
        nameof(DropDownMenu), typeof(ContextMenu), typeof(ToggleSplitButton),
        new PropertyMetadata(null, (d, e) =>
        {
            var button = (ToggleSplitButton)d;
            DropDownHelper.Hook((ContextMenu?)e.OldValue, (ContextMenu?)e.NewValue, button.OnMenuOpened, button.OnMenuClosed);
        }));

    /// <summary>The menu shown below the button when the chevron is clicked.</summary>
    public ContextMenu? DropDownMenu
    {
        get => (ContextMenu?)GetValue(DropDownMenuProperty);
        set => SetValue(DropDownMenuProperty, value);
    }

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen), typeof(bool), typeof(ToggleSplitButton), new PropertyMetadata(false));

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set
        {
            if (DropDownMenu is { } menu && menu.IsOpen != value) DropDownHelper.Toggle(this, menu, fromClick: false);
        }
    }

    public override void OnApplyTemplate()
    {
        if (_arrow != null) _arrow.Click -= OnArrowClick;
        base.OnApplyTemplate();
        _arrow = GetTemplateChild("PART_ArrowButton") as ButtonBase;
        if (_arrow != null) _arrow.Click += OnArrowClick;
    }

    private void OnArrowClick(object sender, RoutedEventArgs e)
    {
        DropDownHelper.Toggle(this, DropDownMenu);
        e.Handled = true;
    }

    private void OnMenuOpened(object sender, RoutedEventArgs e) => SetCurrentValue(IsDropDownOpenProperty, true);

    private void OnMenuClosed(object sender, RoutedEventArgs e) => SetCurrentValue(IsDropDownOpenProperty, false);
}
