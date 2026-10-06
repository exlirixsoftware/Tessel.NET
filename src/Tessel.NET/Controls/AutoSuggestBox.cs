using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Tessel.NET.Controls;

/// <summary>Why <see cref="AutoSuggestBox.TextChanged"/> was raised.</summary>
public enum AutoSuggestionBoxTextChangeReason
{
    /// <summary>The user typed or pasted text.</summary>
    UserInput,

    /// <summary>The <see cref="AutoSuggestBox.Text"/> property was set from code.</summary>
    ProgrammaticChange,

    /// <summary>The user chose a suggestion and the text was replaced by it.</summary>
    SuggestionChosen,
}

public sealed class AutoSuggestBoxTextChangedEventArgs(AutoSuggestionBoxTextChangeReason reason) : EventArgs
{
    public AutoSuggestionBoxTextChangeReason Reason { get; } = reason;
}

public sealed class AutoSuggestBoxSuggestionChosenEventArgs(object selectedItem) : EventArgs
{
    public object SelectedItem { get; } = selectedItem;
}

public sealed class AutoSuggestBoxQuerySubmittedEventArgs(string queryText, object? chosenSuggestion) : EventArgs
{
    public string QueryText { get; } = queryText;

    /// <summary>The suggestion the user picked, or null when the query was typed text.</summary>
    public object? ChosenSuggestion { get; } = chosenSuggestion;
}

/// <summary>
/// A text box that shows a list of suggestions while the user types.
/// Handle <see cref="TextChanged"/> to update <see cref="ItemsSource"/> and <see cref="QuerySubmitted"/> to run the search.
/// Up/Down move through the suggestions, Enter submits, Escape closes the list.
/// </summary>
[TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_QueryButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_SuggestionsList", Type = typeof(ListBox))]
public class AutoSuggestBox : Control
{
    private TextBox? _textBox;
    private ButtonBase? _queryButton;
    private ListBox? _list;
    private bool _syncing;

    static AutoSuggestBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoSuggestBox), new FrameworkPropertyMetadata(typeof(AutoSuggestBox)));
        FocusableProperty.OverrideMetadata(typeof(AutoSuggestBox), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(AutoSuggestBox), new FrameworkPropertyMetadata(false));
    }

    #region Dependency properties

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(AutoSuggestBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(AutoSuggestBox), new PropertyMetadata(string.Empty));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(AutoSuggestBox), new PropertyMetadata(null, OnItemsSourceChanged));

    /// <summary>The suggestions to show. Replace it from the <see cref="TextChanged"/> handler.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
        nameof(DisplayMemberPath), typeof(string), typeof(AutoSuggestBox), new PropertyMetadata(string.Empty));

    /// <summary>Name of a property of the suggestion that is shown and used as the new text (simple property names only).</summary>
    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(AutoSuggestBox), new PropertyMetadata(null));

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly DependencyProperty IsSuggestionListOpenProperty = DependencyProperty.Register(
        nameof(IsSuggestionListOpen), typeof(bool), typeof(AutoSuggestBox), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsSuggestionListOpen
    {
        get => (bool)GetValue(IsSuggestionListOpenProperty);
        set => SetValue(IsSuggestionListOpenProperty, value);
    }

    public static readonly DependencyProperty MaxSuggestionListHeightProperty = DependencyProperty.Register(
        nameof(MaxSuggestionListHeight), typeof(double), typeof(AutoSuggestBox), new PropertyMetadata(280d));

    public double MaxSuggestionListHeight
    {
        get => (double)GetValue(MaxSuggestionListHeightProperty);
        set => SetValue(MaxSuggestionListHeightProperty, value);
    }

    /// <summary>Replace the text with the chosen suggestion (default true).</summary>
    public static readonly DependencyProperty UpdateTextOnSelectProperty = DependencyProperty.Register(
        nameof(UpdateTextOnSelect), typeof(bool), typeof(AutoSuggestBox), new PropertyMetadata(true));

    public bool UpdateTextOnSelect
    {
        get => (bool)GetValue(UpdateTextOnSelectProperty);
        set => SetValue(UpdateTextOnSelectProperty, value);
    }

    /// <summary>Glyph of the search button on the right (empty hides it).</summary>
    public static readonly DependencyProperty QueryIconProperty = DependencyProperty.Register(
        nameof(QueryIcon), typeof(string), typeof(AutoSuggestBox), new PropertyMetadata(""));

    public string QueryIcon
    {
        get => (string)GetValue(QueryIconProperty);
        set => SetValue(QueryIconProperty, value);
    }

    #endregion

    public event EventHandler<AutoSuggestBoxTextChangedEventArgs>? TextChanged;
    public event EventHandler<AutoSuggestBoxSuggestionChosenEventArgs>? SuggestionChosen;
    public event EventHandler<AutoSuggestBoxQuerySubmittedEventArgs>? QuerySubmitted;

    public override void OnApplyTemplate()
    {
        if (_textBox != null)
        {
            _textBox.TextChanged -= OnInnerTextChanged;
            _textBox.PreviewKeyDown -= OnTextBoxKeyDown;
            _textBox.GotKeyboardFocus -= OnTextBoxGotFocus;
        }
        if (_queryButton != null) _queryButton.Click -= OnQueryButtonClick;
        if (_list != null) _list.PreviewMouseLeftButtonUp -= OnListMouseUp;

        base.OnApplyTemplate();

        _textBox = GetTemplateChild("PART_TextBox") as TextBox;
        _queryButton = GetTemplateChild("PART_QueryButton") as ButtonBase;
        _list = GetTemplateChild("PART_SuggestionsList") as ListBox;

        if (_textBox != null)
        {
            _textBox.TextChanged += OnInnerTextChanged;
            _textBox.PreviewKeyDown += OnTextBoxKeyDown;
            _textBox.GotKeyboardFocus += OnTextBoxGotFocus;
        }
        if (_queryButton != null) _queryButton.Click += OnQueryButtonClick;
        if (_list != null) _list.PreviewMouseLeftButtonUp += OnListMouseUp;

        SyncTextBox();
    }

    /// <summary>Moves keyboard focus into the text box.</summary>
    public new bool Focus() => _textBox?.Focus() ?? false;

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (AutoSuggestBox)d;
        if (box._syncing) return;
        box.SyncTextBox();
        box.TextChanged?.Invoke(box, new AutoSuggestBoxTextChangedEventArgs(AutoSuggestionBoxTextChangeReason.ProgrammaticChange));
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (AutoSuggestBox)d;
        if (box._list != null) box._list.SelectedIndex = -1;
        if (box._textBox is { IsKeyboardFocused: true } && !string.IsNullOrEmpty(box.Text))
        {
            box.IsSuggestionListOpen = HasAny(e.NewValue as IEnumerable);
        }
        else if (!HasAny(e.NewValue as IEnumerable))
        {
            box.IsSuggestionListOpen = false;
        }
    }

    private void OnInnerTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || _textBox == null || _textBox.Text == Text) return;

        _syncing = true;
        try
        {
            SetCurrentValue(TextProperty, _textBox.Text);
            TextChanged?.Invoke(this, new AutoSuggestBoxTextChangedEventArgs(AutoSuggestionBoxTextChangeReason.UserInput));
        }
        finally
        {
            _syncing = false;
        }

        if (string.IsNullOrEmpty(Text)) IsSuggestionListOpen = false;
        else if (HasAny(ItemsSource)) IsSuggestionListOpen = true;
    }

    private void SyncTextBox()
    {
        if (_textBox == null || _textBox.Text == Text) return;
        _syncing = true;
        try
        {
            _textBox.Text = Text;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnTextBoxGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(Text) && HasAny(ItemsSource)) IsSuggestionListOpen = true;
    }

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (!IsSuggestionListOpen && HasAny(ItemsSource)) IsSuggestionListOpen = true;
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                Submit(IsSuggestionListOpen ? _list?.SelectedItem : null);
                e.Handled = true;
                break;
            case Key.Escape:
                if (IsSuggestionListOpen)
                {
                    IsSuggestionListOpen = false;
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnQueryButtonClick(object sender, RoutedEventArgs e) => Submit(null);

    private void OnListMouseUp(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not ListBoxItem) source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        if (source is not ListBoxItem item || _list == null) return;

        var data = _list.ItemContainerGenerator.ItemFromContainer(item);
        if (data == DependencyProperty.UnsetValue) return;

        Submit(data);
        e.Handled = true;
    }

    private void MoveSelection(int delta)
    {
        if (_list is not { Items.Count: > 0 }) return;
        var index = _list.SelectedIndex + delta;
        if (index < -1) index = _list.Items.Count - 1;
        if (index >= _list.Items.Count) index = -1;
        _list.SelectedIndex = index;
        if (index >= 0) _list.ScrollIntoView(_list.SelectedItem);
    }

    private void Submit(object? chosen)
    {
        if (chosen != null)
        {
            SuggestionChosen?.Invoke(this, new AutoSuggestBoxSuggestionChosenEventArgs(chosen));
            if (UpdateTextOnSelect) SetTextFromSuggestion(GetItemText(chosen));
        }

        IsSuggestionListOpen = false;
        QuerySubmitted?.Invoke(this, new AutoSuggestBoxQuerySubmittedEventArgs(Text, chosen));
        _textBox?.Focus();
        _textBox?.Select(Text.Length, 0);
    }

    private void SetTextFromSuggestion(string text)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(TextProperty, text);
            if (_textBox != null) _textBox.Text = text;
        }
        finally
        {
            _syncing = false;
        }
        TextChanged?.Invoke(this, new AutoSuggestBoxTextChangedEventArgs(AutoSuggestionBoxTextChangeReason.SuggestionChosen));
    }

    private string GetItemText(object item)
    {
        if (!string.IsNullOrEmpty(DisplayMemberPath))
        {
            var property = item.GetType().GetProperty(DisplayMemberPath);
            if (property != null) return property.GetValue(item)?.ToString() ?? string.Empty;
        }
        return item.ToString() ?? string.Empty;
    }

    private static bool HasAny(IEnumerable? items)
    {
        if (items == null) return false;
        if (items is ICollection collection) return collection.Count > 0;
        var enumerator = items.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }
}
