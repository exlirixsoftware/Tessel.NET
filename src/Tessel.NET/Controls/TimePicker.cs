using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Tessel.NET.Controls;

/// <summary>
/// A time-of-day picker with hour / minute (/ AM-PM) columns in a drop-down.
/// <code>&lt;tessel:TimePicker SelectedTime="{Binding Alarm}" Is24Hour="True" /&gt;</code>
/// </summary>
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_HourList", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_MinuteList", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_PeriodList", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_AcceptButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_CancelButton", Type = typeof(ButtonBase))]
public class TimePicker : Control
{
    private Popup? _popup;
    private ListBox? _hours;
    private ListBox? _minutes;
    private ListBox? _periods;
    private ButtonBase? _accept;
    private ButtonBase? _cancel;

    static TimePicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TimePicker), new FrameworkPropertyMetadata(typeof(TimePicker)));
    }

    #region Dependency properties

    public static readonly DependencyProperty SelectedTimeProperty = DependencyProperty.Register(
        nameof(SelectedTime), typeof(TimeSpan?), typeof(TimePicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedTimeChanged));

    /// <summary>The selected time of day, or null when nothing is selected.</summary>
    public TimeSpan? SelectedTime
    {
        get => (TimeSpan?)GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    public static readonly DependencyProperty Is24HourProperty = DependencyProperty.Register(
        nameof(Is24Hour), typeof(bool), typeof(TimePicker),
        new PropertyMetadata(!CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('t'), OnFormatChanged));

    /// <summary>Use a 24-hour clock (default follows the current culture).</summary>
    public bool Is24Hour
    {
        get => (bool)GetValue(Is24HourProperty);
        set => SetValue(Is24HourProperty, value);
    }

    public static readonly DependencyProperty MinuteIncrementProperty = DependencyProperty.Register(
        nameof(MinuteIncrement), typeof(int), typeof(TimePicker), new PropertyMetadata(1, OnFormatChanged, (_, v) => Math.Clamp((int)v, 1, 30)));

    /// <summary>Step of the minute column (1 to 30).</summary>
    public int MinuteIncrement
    {
        get => (int)GetValue(MinuteIncrementProperty);
        set => SetValue(MinuteIncrementProperty, value);
    }

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen), typeof(bool), typeof(TimePicker),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(TimePicker), new PropertyMetadata("Select a time"));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    private static readonly DependencyPropertyKey DisplayTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(DisplayText), typeof(string), typeof(TimePicker), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DisplayTextProperty = DisplayTextPropertyKey.DependencyProperty;

    /// <summary>The selected time formatted for display (empty when nothing is selected).</summary>
    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        private set => SetValue(DisplayTextPropertyKey, value);
    }

    public static readonly RoutedEvent SelectedTimeChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectedTimeChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<TimeSpan?>), typeof(TimePicker));

    public event RoutedPropertyChangedEventHandler<TimeSpan?> SelectedTimeChanged
    {
        add => AddHandler(SelectedTimeChangedEvent, value);
        remove => RemoveHandler(SelectedTimeChangedEvent, value);
    }

    #endregion

    public override void OnApplyTemplate()
    {
        if (_accept != null) _accept.Click -= OnAcceptClick;
        if (_cancel != null) _cancel.Click -= OnCancelClick;

        base.OnApplyTemplate();

        _popup = GetTemplateChild("PART_Popup") as Popup;
        _hours = GetTemplateChild("PART_HourList") as ListBox;
        _minutes = GetTemplateChild("PART_MinuteList") as ListBox;
        _periods = GetTemplateChild("PART_PeriodList") as ListBox;
        _accept = GetTemplateChild("PART_AcceptButton") as ButtonBase;
        _cancel = GetTemplateChild("PART_CancelButton") as ButtonBase;

        if (_accept != null) _accept.Click += OnAcceptClick;
        if (_cancel != null) _cancel.Click += OnCancelClick;

        FillLists();
        UpdateDisplayText();
    }

    private static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (TimePicker)d;
        picker.UpdateDisplayText();
        picker.RaiseEvent(new RoutedPropertyChangedEventArgs<TimeSpan?>((TimeSpan?)e.OldValue, (TimeSpan?)e.NewValue, SelectedTimeChangedEvent));
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (TimePicker)d;
        picker.FillLists();
        picker.UpdateDisplayText();
    }

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (TimePicker)d;
        if ((bool)e.NewValue) picker.SyncListsToSelection();
    }

    private void UpdateDisplayText()
    {
        if (SelectedTime is not { } time)
        {
            DisplayText = string.Empty;
            return;
        }
        var value = DateTime.Today.Add(time);
        DisplayText = value.ToString(Is24Hour ? "HH:mm" : "h:mm tt", CultureInfo.CurrentCulture);
    }

    private void FillLists()
    {
        if (_hours == null || _minutes == null) return;

        _hours.Items.Clear();
        if (Is24Hour)
        {
            for (var h = 0; h < 24; h++) _hours.Items.Add(h.ToString("00"));
        }
        else
        {
            for (var h = 1; h <= 12; h++) _hours.Items.Add(h.ToString());
        }

        _minutes.Items.Clear();
        for (var m = 0; m < 60; m += MinuteIncrement) _minutes.Items.Add(m.ToString("00"));

        if (_periods != null)
        {
            _periods.Items.Clear();
            _periods.Items.Add(CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator is { Length: > 0 } am ? am : "AM");
            _periods.Items.Add(CultureInfo.CurrentCulture.DateTimeFormat.PMDesignator is { Length: > 0 } pm ? pm : "PM");
            _periods.Visibility = Is24Hour ? Visibility.Collapsed : Visibility.Visible;
        }

        SyncListsToSelection();
    }

    private void SyncListsToSelection()
    {
        if (_hours == null || _minutes == null) return;

        var time = SelectedTime ?? DateTime.Now.TimeOfDay;
        var hour = time.Hours;
        var minute = time.Minutes - time.Minutes % MinuteIncrement;

        if (Is24Hour)
        {
            _hours.SelectedIndex = hour;
        }
        else
        {
            var hour12 = hour % 12;
            _hours.SelectedIndex = hour12 == 0 ? 11 : hour12 - 1;
            if (_periods != null) _periods.SelectedIndex = hour >= 12 ? 1 : 0;
        }
        _minutes.SelectedIndex = minute / MinuteIncrement;

        _hours.ScrollIntoView(_hours.SelectedItem);
        _minutes.ScrollIntoView(_minutes.SelectedItem);
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (_hours is { SelectedIndex: >= 0 } && _minutes is { SelectedIndex: >= 0 })
        {
            var hour = Is24Hour ? _hours.SelectedIndex : (_hours.SelectedIndex + 1) % 12 + (_periods?.SelectedIndex == 1 ? 12 : 0);
            var minute = _minutes.SelectedIndex * MinuteIncrement;
            SetCurrentValue(SelectedTimeProperty, new TimeSpan(hour, minute, 0));
        }
        IsDropDownOpen = false;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => IsDropDownOpen = false;
}
