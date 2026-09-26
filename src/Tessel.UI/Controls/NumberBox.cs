using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Tessel.UI.Controls;

/// <summary>A numeric input with spin buttons, keyboard (Up/Down/PageUp/PageDown) and mouse-wheel stepping.</summary>
[TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_IncreaseButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_DecreaseButton", Type = typeof(ButtonBase))]
public class NumberBox : Control
{
    private TextBox? _textBox;
    private ButtonBase? _increaseButton;
    private ButtonBase? _decreaseButton;

    static NumberBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NumberBox), new FrameworkPropertyMetadata(typeof(NumberBox)));
        FocusableProperty.OverrideMetadata(typeof(NumberBox), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(NumberBox), new FrameworkPropertyMetadata(false));
    }

    #region Dependency properties

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(NumberBox),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(NumberBox), new PropertyMetadata(double.MinValue, OnRangeChanged));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(NumberBox), new PropertyMetadata(double.MaxValue, OnRangeChanged));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty SmallChangeProperty = DependencyProperty.Register(
        nameof(SmallChange), typeof(double), typeof(NumberBox), new PropertyMetadata(1d));

    public double SmallChange
    {
        get => (double)GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public static readonly DependencyProperty LargeChangeProperty = DependencyProperty.Register(
        nameof(LargeChange), typeof(double), typeof(NumberBox), new PropertyMetadata(10d));

    public double LargeChange
    {
        get => (double)GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    /// <summary>Numeric format used to display the value (default "0.##").</summary>
    public static readonly DependencyProperty StringFormatProperty = DependencyProperty.Register(
        nameof(StringFormat), typeof(string), typeof(NumberBox), new PropertyMetadata("0.##", (d, _) => ((NumberBox)d).UpdateText()));

    public string StringFormat
    {
        get => (string)GetValue(StringFormatProperty);
        set => SetValue(StringFormatProperty, value);
    }

    public static readonly DependencyProperty SpinButtonsVisibilityProperty = DependencyProperty.Register(
        nameof(SpinButtonsVisibility), typeof(Visibility), typeof(NumberBox), new PropertyMetadata(Visibility.Visible));

    public Visibility SpinButtonsVisibility
    {
        get => (Visibility)GetValue(SpinButtonsVisibilityProperty);
        set => SetValue(SpinButtonsVisibilityProperty, value);
    }

    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<double>), typeof(NumberBox));

    public event RoutedPropertyChangedEventHandler<double> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    #endregion

    public override void OnApplyTemplate()
    {
        if (_textBox != null)
        {
            _textBox.LostKeyboardFocus -= OnTextBoxLostFocus;
            _textBox.PreviewKeyDown -= OnTextBoxKeyDown;
        }
        if (_increaseButton != null) _increaseButton.Click -= OnIncrease;
        if (_decreaseButton != null) _decreaseButton.Click -= OnDecrease;

        base.OnApplyTemplate();

        _textBox = GetTemplateChild("PART_TextBox") as TextBox;
        _increaseButton = GetTemplateChild("PART_IncreaseButton") as ButtonBase;
        _decreaseButton = GetTemplateChild("PART_DecreaseButton") as ButtonBase;

        if (_textBox != null)
        {
            _textBox.LostKeyboardFocus += OnTextBoxLostFocus;
            _textBox.PreviewKeyDown += OnTextBoxKeyDown;
        }
        if (_increaseButton != null) _increaseButton.Click += OnIncrease;
        if (_decreaseButton != null) _decreaseButton.Click += OnDecrease;

        UpdateText();
    }

    /// <summary>Adds <paramref name="delta"/> to the value, clamped to the range.</summary>
    public void Step(double delta)
    {
        Commit();
        SetCurrentValue(ValueProperty, Value + delta);
        UpdateText();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!IsKeyboardFocusWithin || e.Handled) return;
        Step(e.Delta > 0 ? SmallChange : -SmallChange);
        e.Handled = true;
    }

    private void OnIncrease(object sender, RoutedEventArgs e) => Step(SmallChange);

    private void OnDecrease(object sender, RoutedEventArgs e) => Step(-SmallChange);

    private void OnTextBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e) => Commit();

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Commit();
                _textBox?.SelectAll();
                e.Handled = true;
                break;
            case Key.Up:
                Step(SmallChange);
                e.Handled = true;
                break;
            case Key.Down:
                Step(-SmallChange);
                e.Handled = true;
                break;
            case Key.PageUp:
                Step(LargeChange);
                e.Handled = true;
                break;
            case Key.PageDown:
                Step(-LargeChange);
                e.Handled = true;
                break;
            case Key.Escape:
                UpdateText();
                e.Handled = true;
                break;
        }
    }

    private void Commit()
    {
        if (_textBox == null) return;
        if (double.TryParse(_textBox.Text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var parsed))
        {
            SetCurrentValue(ValueProperty, parsed);
        }
        UpdateText();
    }

    private void UpdateText()
    {
        if (_textBox == null) return;
        _textBox.Text = Value.ToString(StringFormat, CultureInfo.CurrentCulture);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var box = (NumberBox)d;
        var value = (double)baseValue;
        if (double.IsNaN(value)) return box.Minimum > double.MinValue ? box.Minimum : 0d;
        return Math.Clamp(value, box.Minimum, Math.Max(box.Minimum, box.Maximum));
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => d.CoerceValue(ValueProperty);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (NumberBox)d;
        if (box._textBox is { IsKeyboardFocused: false }) box.UpdateText();
        box.RaiseEvent(new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue, ValueChangedEvent));
    }
}
