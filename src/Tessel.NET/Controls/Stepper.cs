using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Tessel.NET.Controls;

/// <summary>The progress state of a <see cref="StepperItem"/>.</summary>
public enum StepState
{
    /// <summary>A step that has not been reached yet.</summary>
    Pending,

    /// <summary>The step the user is on.</summary>
    Current,

    /// <summary>A step that is done.</summary>
    Completed,

    /// <summary>A step that failed (see <see cref="StepperItem.IsError"/>).</summary>
    Error,
}

/// <summary>
/// Shows progress through a sequence of steps (a wizard, an order status, a timeline).
/// Set <see cref="CurrentStep"/>; earlier steps are shown as completed, later ones as pending.
/// Use <see cref="Orientation.Vertical"/> for a timeline layout.
/// <code>&lt;tessel:Stepper CurrentStep="1"&gt;&lt;tessel:StepperItem Header="Cart" /&gt;&lt;tessel:StepperItem Header="Payment" /&gt;&lt;/tessel:Stepper&gt;</code>
/// </summary>
public class Stepper : ItemsControl
{
    static Stepper()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Stepper), new FrameworkPropertyMetadata(typeof(Stepper)));
        FocusableProperty.OverrideMetadata(typeof(Stepper), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Stepper), new FrameworkPropertyMetadata(false));
    }

    /// <summary>Zero-based index of the current step. Use a value past the last step to mark all steps as completed.</summary>
    public static readonly DependencyProperty CurrentStepProperty = DependencyProperty.Register(
        nameof(CurrentStep), typeof(int), typeof(Stepper),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStepOrOrientationChanged, (_, v) => Math.Max(0, (int)v)));

    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(Stepper),
        new PropertyMetadata(Orientation.Horizontal, OnStepOrOrientationChanged));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is StepperItem;

    protected override DependencyObject GetContainerForItemOverride() => new StepperItem();

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

    private static void OnStepOrOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Stepper)d).ScheduleUpdate();

    private void ScheduleUpdate() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateItems);

    private void UpdateItems()
    {
        var count = Items.Count;
        for (var i = 0; i < count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is not StepperItem item) continue;

            item.Index = i + 1;
            item.IsLast = i == count - 1;
            item.Orientation = Orientation;
            item.State = item.IsError ? StepState.Error
                : i < CurrentStep ? StepState.Completed
                : i == CurrentStep ? StepState.Current
                : StepState.Pending;
        }
    }

    internal void NotifyItemChanged() => ScheduleUpdate();
}

/// <summary>One step of a <see cref="Stepper"/>. The content (if any) is shown below the header and description.</summary>
public class StepperItem : ContentControl
{
    static StepperItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StepperItem), new FrameworkPropertyMetadata(typeof(StepperItem)));
        FocusableProperty.OverrideMetadata(typeof(StepperItem), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(StepperItem), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(StepperItem), new PropertyMetadata(null));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(object), typeof(StepperItem), new PropertyMetadata(null));

    public object? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Marks the step as failed.</summary>
    public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register(
        nameof(IsError), typeof(bool), typeof(StepperItem),
        new PropertyMetadata(false, (d, _) => (ItemsControl.ItemsControlFromItemContainer(d) as Stepper)?.NotifyItemChanged()));

    public bool IsError
    {
        get => (bool)GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    private static readonly DependencyPropertyKey StatePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(State), typeof(StepState), typeof(StepperItem), new PropertyMetadata(StepState.Pending));

    public static readonly DependencyProperty StateProperty = StatePropertyKey.DependencyProperty;

    /// <summary>Set by the owning <see cref="Stepper"/> from <see cref="Stepper.CurrentStep"/> and <see cref="IsError"/>.</summary>
    public StepState State
    {
        get => (StepState)GetValue(StateProperty);
        internal set => SetValue(StatePropertyKey, value);
    }

    private static readonly DependencyPropertyKey IndexPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Index), typeof(int), typeof(StepperItem), new PropertyMetadata(1));

    public static readonly DependencyProperty IndexProperty = IndexPropertyKey.DependencyProperty;

    /// <summary>The 1-based number of the step.</summary>
    public int Index
    {
        get => (int)GetValue(IndexProperty);
        internal set => SetValue(IndexPropertyKey, value);
    }

    private static readonly DependencyPropertyKey IsLastPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsLast), typeof(bool), typeof(StepperItem), new PropertyMetadata(false));

    public static readonly DependencyProperty IsLastProperty = IsLastPropertyKey.DependencyProperty;

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        internal set => SetValue(IsLastPropertyKey, value);
    }

    private static readonly DependencyPropertyKey OrientationPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Orientation), typeof(Orientation), typeof(StepperItem), new PropertyMetadata(Orientation.Horizontal));

    public static readonly DependencyProperty OrientationProperty = OrientationPropertyKey.DependencyProperty;

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        internal set => SetValue(OrientationPropertyKey, value);
    }
}
