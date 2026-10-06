using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Tessel.NET.Controls;

/// <summary>
/// A star rating. Click a star to set <see cref="Value"/>; click the current value again to clear it
/// (when <see cref="IsClearEnabled"/>). Arrow keys change the value as well.
/// </summary>
[TemplatePart(Name = "PART_Stars", Type = typeof(Panel))]
public class RatingControl : Control
{
    private const string FilledStar = "";
    private const string EmptyStar = "";

    private Panel? _stars;
    private int _hover;

    static RatingControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RatingControl), new FrameworkPropertyMetadata(typeof(RatingControl)));
    }

    #region Dependency properties

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RatingControl),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    /// <summary>The rating from 0 (none) to <see cref="MaxRating"/>; whole numbers only.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MaxRatingProperty = DependencyProperty.Register(
        nameof(MaxRating), typeof(int), typeof(RatingControl),
        new PropertyMetadata(5, OnMaxRatingChanged, (_, v) => Math.Clamp((int)v, 1, 20)));

    public int MaxRating
    {
        get => (int)GetValue(MaxRatingProperty);
        set => SetValue(MaxRatingProperty, value);
    }

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(RatingControl), new PropertyMetadata(false, (d, _) => ((RatingControl)d).UpdateStars()));

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public static readonly DependencyProperty IsClearEnabledProperty = DependencyProperty.Register(
        nameof(IsClearEnabled), typeof(bool), typeof(RatingControl), new PropertyMetadata(true));

    public bool IsClearEnabled
    {
        get => (bool)GetValue(IsClearEnabledProperty);
        set => SetValue(IsClearEnabledProperty, value);
    }

    /// <summary>Text shown next to the stars, e.g. "4 of 5".</summary>
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(RatingControl), new PropertyMetadata(string.Empty));

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<double>), typeof(RatingControl));

    public event RoutedPropertyChangedEventHandler<double> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    #endregion

    public override void OnApplyTemplate()
    {
        if (_stars != null)
        {
            _stars.MouseMove -= OnStarsMouseMove;
            _stars.MouseLeave -= OnStarsMouseLeave;
            _stars.MouseLeftButtonUp -= OnStarsMouseUp;
        }

        base.OnApplyTemplate();

        _stars = GetTemplateChild("PART_Stars") as Panel;
        if (_stars != null)
        {
            _stars.MouseMove += OnStarsMouseMove;
            _stars.MouseLeave += OnStarsMouseLeave;
            _stars.MouseLeftButtonUp += OnStarsMouseUp;
        }
        BuildStars();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsReadOnly || e.Handled) return;
        switch (e.Key)
        {
            case Key.Right or Key.Up:
                Value++;
                e.Handled = true;
                break;
            case Key.Left or Key.Down:
                Value--;
                e.Handled = true;
                break;
            case Key.Home or Key.Delete:
                if (IsClearEnabled) Value = 0;
                e.Handled = true;
                break;
            case Key.End:
                Value = MaxRating;
                e.Handled = true;
                break;
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (!IsReadOnly) Focus();
    }

    private static object CoerceValue(DependencyObject d, object value)
    {
        var rating = (RatingControl)d;
        return Math.Clamp(Math.Round((double)value), 0, rating.MaxRating);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var rating = (RatingControl)d;
        rating.UpdateStars();
        rating.RaiseEvent(new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue, ValueChangedEvent));
    }

    private static void OnMaxRatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var rating = (RatingControl)d;
        rating.CoerceValue(ValueProperty);
        rating.BuildStars();
    }

    private void BuildStars()
    {
        if (_stars == null) return;
        _stars.Children.Clear();

        var iconFont = TryFindResource("Tessel.IconFontFamily") as FontFamily;
        for (var i = 0; i < MaxRating; i++)
        {
            var star = new TextBlock
            {
                Text = EmptyStar,
                Margin = new Thickness(0, 0, 4, 0),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (iconFont != null) star.FontFamily = iconFont;
            star.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(FontSize)) { Source = this });
            _stars.Children.Add(star);
        }
        UpdateStars();
    }

    private void UpdateStars()
    {
        if (_stars == null) return;
        var shown = _hover > 0 && !IsReadOnly ? _hover : (int)Value;
        for (var i = 0; i < _stars.Children.Count; i++)
        {
            var star = (TextBlock)_stars.Children[i];
            var filled = i < shown;
            star.Text = filled ? FilledStar : EmptyStar;
            if (filled) star.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
            else star.SetResourceReference(TextBlock.ForegroundProperty, "Tessel.TextTertiaryBrush");
        }
        Cursor = IsReadOnly ? null : Cursors.Hand;
    }

    private int StarAt(MouseEventArgs e)
    {
        for (var i = 0; i < (_stars?.Children.Count ?? 0); i++)
        {
            var child = (FrameworkElement)_stars!.Children[i];
            var position = e.GetPosition(child);
            // Count the gap after a star as part of it so moving across the row has no dead zones.
            if (position.X >= 0 && position.X <= child.ActualWidth + child.Margin.Right && position.Y >= 0 && position.Y <= child.ActualHeight) return i + 1;
        }
        return 0;
    }

    private void OnStarsMouseMove(object sender, MouseEventArgs e)
    {
        if (IsReadOnly) return;
        var hover = StarAt(e);
        if (hover == _hover) return;
        _hover = hover;
        UpdateStars();
    }

    private void OnStarsMouseLeave(object sender, MouseEventArgs e)
    {
        _hover = 0;
        UpdateStars();
    }

    private void OnStarsMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (IsReadOnly) return;
        var star = StarAt(e);
        if (star == 0) return;
        Value = IsClearEnabled && star == (int)Value ? 0 : star;
        e.Handled = true;
    }
}
