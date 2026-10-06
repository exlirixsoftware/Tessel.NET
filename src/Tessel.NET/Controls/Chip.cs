using System;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Tessel.NET.Controls;

/// <summary>
/// A compact label for tags, filters and selections. It can be removable (an "x" button) and,
/// when <see cref="IsSelectable"/> is true, toggles like a check box (<see cref="ToggleButton.IsChecked"/>).
/// </summary>
[TemplatePart(Name = "PART_RemoveButton", Type = typeof(ButtonBase))]
public class Chip : ToggleButton
{
    private ButtonBase? _removeButton;

    static Chip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Chip), new FrameworkPropertyMetadata(typeof(Chip)));
    }

    /// <summary>Shows an "x" button that raises <see cref="Removed"/>.</summary>
    public static readonly DependencyProperty IsRemovableProperty = DependencyProperty.Register(
        nameof(IsRemovable), typeof(bool), typeof(Chip), new PropertyMetadata(false));

    public bool IsRemovable
    {
        get => (bool)GetValue(IsRemovableProperty);
        set => SetValue(IsRemovableProperty, value);
    }

    /// <summary>When true, clicking the chip toggles <see cref="ToggleButton.IsChecked"/> (a filter chip).</summary>
    public static readonly DependencyProperty IsSelectableProperty = DependencyProperty.Register(
        nameof(IsSelectable), typeof(bool), typeof(Chip), new PropertyMetadata(false));

    public bool IsSelectable
    {
        get => (bool)GetValue(IsSelectableProperty);
        set => SetValue(IsSelectableProperty, value);
    }

    /// <summary>A glyph (use <c>{tessel:Glyph ...}</c>) or any element shown before the content.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(Chip), new PropertyMetadata(null));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Raised when the "x" button is clicked. The chip is not removed automatically.</summary>
    public event EventHandler? Removed;

    public override void OnApplyTemplate()
    {
        if (_removeButton != null) _removeButton.Click -= OnRemoveClick;
        base.OnApplyTemplate();
        _removeButton = GetTemplateChild("PART_RemoveButton") as ButtonBase;
        if (_removeButton != null) _removeButton.Click += OnRemoveClick;
    }

    protected override void OnToggle()
    {
        if (IsSelectable) base.OnToggle();
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        Removed?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
