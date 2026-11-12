using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Tessel.NET.Controls;

/// <summary>
/// A text box that turns what you type into tags (chips): labels on a post, recipients of a message, keywords. Enter, Tab or a delimiter
/// (a comma) adds the tag, Backspace in an empty box removes the last one, and <see cref="Suggestions"/> are offered as you type.
/// <code>&lt;tessel:TagInput Suggestions="{Binding AllTags}" Tags="{Binding Labels}" /&gt;</code>
/// </summary>
[TemplatePart(Name = "PART_Panel", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_List", Type = typeof(ListBox))]
public class TagInput : Control
{
    private Panel? _panel;
    private Popup? _popup;
    private ListBox? _list;
    private TextBox? _input;
    private INotifyCollectionChanged? _observed;
    private readonly ObservableCollection<string> _matches = [];

    static TagInput()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TagInput), new FrameworkPropertyMetadata(typeof(TagInput)));
        FocusableProperty.OverrideMetadata(typeof(TagInput), new FrameworkPropertyMetadata(false));
    }

    public TagInput()
    {
        SetCurrentValue(TagsProperty, new ObservableCollection<string>());
    }

    public static readonly DependencyProperty TagsProperty = DependencyProperty.Register(
        nameof(Tags), typeof(ObservableCollection<string>), typeof(TagInput),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((TagInput)d).OnTagsChanged((ObservableCollection<string>?)e.NewValue)));

    public static readonly DependencyProperty SuggestionsProperty = DependencyProperty.Register(
        nameof(Suggestions), typeof(IEnumerable<string>), typeof(TagInput), new PropertyMetadata(null));

    public static readonly DependencyProperty AllowDuplicatesProperty = DependencyProperty.Register(
        nameof(AllowDuplicates), typeof(bool), typeof(TagInput), new PropertyMetadata(false));

    public static readonly DependencyProperty AllowCustomTagsProperty = DependencyProperty.Register(
        nameof(AllowCustomTags), typeof(bool), typeof(TagInput), new PropertyMetadata(true));

    public static readonly DependencyProperty MaxTagsProperty = DependencyProperty.Register(
        nameof(MaxTags), typeof(int), typeof(TagInput), new PropertyMetadata(0, (d, _) => ((TagInput)d).Rebuild()));

    public static readonly DependencyProperty DelimitersProperty = DependencyProperty.Register(
        nameof(Delimiters), typeof(string), typeof(TagInput), new PropertyMetadata(",;"));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(TagInput), new PropertyMetadata("Add a tag...", (d, _) => ((TagInput)d).UpdatePlaceholder()));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(TagInput), new PropertyMetadata(false, (d, _) => ((TagInput)d).Rebuild()));

    /// <summary>The tags (an observable collection, so code can add and remove them too).</summary>
    public ObservableCollection<string> Tags
    {
        get => (ObservableCollection<string>)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    /// <summary>Words offered while typing.</summary>
    public IEnumerable<string>? Suggestions
    {
        get => (IEnumerable<string>?)GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    /// <summary>Allows the same tag twice (off by default; tags are compared without regard to case).</summary>
    public bool AllowDuplicates
    {
        get => (bool)GetValue(AllowDuplicatesProperty);
        set => SetValue(AllowDuplicatesProperty, value);
    }

    /// <summary>False accepts only tags that are in <see cref="Suggestions"/>.</summary>
    public bool AllowCustomTags
    {
        get => (bool)GetValue(AllowCustomTagsProperty);
        set => SetValue(AllowCustomTagsProperty, value);
    }

    /// <summary>The most tags that can be added (0 = no limit).</summary>
    public int MaxTags
    {
        get => (int)GetValue(MaxTagsProperty);
        set => SetValue(MaxTagsProperty, value);
    }

    /// <summary>Characters that end a tag when typed.</summary>
    public string Delimiters
    {
        get => (string)GetValue(DelimitersProperty);
        set => SetValue(DelimitersProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>The text typed so far that is not a tag yet.</summary>
    public string PendingText => _input?.Text ?? string.Empty;

    public event EventHandler<string>? TagAdded;

    public event EventHandler<string>? TagRemoved;

    #region Tags

    private bool IsFull => MaxTags > 0 && Tags.Count >= MaxTags;

    /// <summary>Adds a tag; false when it is empty, a duplicate, not allowed, or the limit is reached.</summary>
    public bool AddTag(string? text)
    {
        var tag = (text ?? string.Empty).Trim();
        if (tag.Length == 0 || IsFull || IsReadOnly) return false;
        if (!AllowDuplicates && Tags.Any(t => string.Equals(t, tag, StringComparison.CurrentCultureIgnoreCase))) return false;
        if (!AllowCustomTags)
        {
            var known = Suggestions?.FirstOrDefault(s => string.Equals(s, tag, StringComparison.CurrentCultureIgnoreCase));
            if (known == null) return false;
            tag = known;
        }
        Tags.Add(tag);
        TagAdded?.Invoke(this, tag);
        return true;
    }

    public bool RemoveTag(string tag)
    {
        if (IsReadOnly || !Tags.Remove(tag)) return false;
        TagRemoved?.Invoke(this, tag);
        return true;
    }

    private void OnTagsChanged(ObservableCollection<string>? tags)
    {
        if (_observed != null) _observed.CollectionChanged -= OnTagsCollectionChanged;
        _observed = tags;
        if (_observed != null) _observed.CollectionChanged += OnTagsCollectionChanged;
        Rebuild();
    }

    private void OnTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    #endregion

    #region Template

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _panel = GetTemplateChild("PART_Panel") as Panel;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _list = GetTemplateChild("PART_List") as ListBox;
        if (_list != null)
        {
            _list.ItemsSource = _matches;
            _list.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_list, source) is ListBoxItem { DataContext: string pick })
                {
                    CommitSuggestion(pick);
                    e.Handled = true;
                }
            };
        }

        _input = new TextBox
        {
            Style = null,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            MinWidth = 90,
            Margin = new Thickness(2, 1, 2, 1),
            Padding = new Thickness(2, 3, 2, 3),
            VerticalAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null,
        };
        _input.SetResourceReference(Control.ForegroundProperty, "Tessel.TextBrush");
        _input.SetResourceReference(TextBoxBase.CaretBrushProperty, "Tessel.TextBrush");
        _input.TextChanged += OnInputChanged;
        _input.PreviewKeyDown += OnInputKeyDown;
        _input.LostKeyboardFocus += (_, _) =>
        {
            if (_popup is { IsMouseOver: true } || (_list?.IsMouseOver ?? false)) return;
            if (AllowCustomTags && !string.IsNullOrWhiteSpace(_input.Text) && AddTag(_input.Text)) _input.Clear();
            CloseSuggestions();
        };
        Rebuild();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsReadOnly) _input?.Focus();
    }

    private void UpdatePlaceholder()
    {
        if (_input != null) _input.ToolTip = null;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_panel == null || _input == null || Tags == null) return;
        var text = _input.Text;
        _panel.Children.Clear();
        foreach (var tag in Tags) _panel.Children.Add(Chip(tag));
        if (!IsReadOnly && !IsFull) _panel.Children.Add(_input);
        _input.Text = text;
    }

    private FrameworkElement Chip(string tag)
    {
        var label = new TextBlock { Text = tag, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, IsReadOnly ? 0 : 4, 0) };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Tessel.AccentBrush");
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(label);

        if (!IsReadOnly)
        {
            var close = new Button
            {
                Content = "",
                FontSize = 9,
                Width = 16,
                Height = 16,
                Padding = new Thickness(0),
                Focusable = false,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = "Remove",
                Style = null,
                Template = CloseTemplate(),
            };
            close.SetResourceReference(Control.FontFamilyProperty, "Tessel.IconFontFamily");
            close.SetResourceReference(Control.ForegroundProperty, "Tessel.AccentBrush");
            close.Click += (_, _) =>
            {
                RemoveTag(tag);
                _input?.Focus();
            };
            row.Children.Add(close);
        }

        var chip = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 2, IsReadOnly ? 10 : 4, 2),
            Margin = new Thickness(2),
            Child = row,
        };
        chip.SetResourceReference(Border.BackgroundProperty, "Tessel.AccentSubtleBrush");
        return chip;
    }

    private static ControlTemplate CloseTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        return template;
    }

    #endregion

    #region Typing

    private void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        if (_input == null) return;
        var text = _input.Text;
        var delimiters = Delimiters ?? string.Empty;
        if (delimiters.Length > 0 && text.IndexOfAny(delimiters.ToCharArray()) >= 0)
        {
            // typed (a delimiter at the end) or pasted (anywhere) text may hold several tags
            var parts = text.Split(delimiters.ToCharArray());
            var rest = parts[^1];
            foreach (var part in parts[..^1]) AddTag(part);
            _input.Text = rest;
            _input.CaretIndex = rest.Length;
            return;
        }
        UpdateSuggestions();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (_input == null) return;
        var open = _popup?.IsOpen == true && _matches.Count > 0;

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Tab when !string.IsNullOrWhiteSpace(_input.Text):
                if (open && _list?.SelectedItem is string chosen && !string.IsNullOrWhiteSpace(_input.Text)) CommitSuggestion(chosen);
                else if (AddTag(_input.Text)) _input.Clear();
                else if (e.Key == Key.Tab) return;
                CloseSuggestions();
                e.Handled = true;
                break;
            case Key.Back when _input.Text.Length == 0 && Tags.Count > 0:
                RemoveTag(Tags[^1]);
                e.Handled = true;
                break;
            case Key.Down when open:
                if (_list != null) _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _matches.Count - 1);
                e.Handled = true;
                break;
            case Key.Up when open:
                if (_list != null) _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0);
                e.Handled = true;
                break;
            case Key.Escape when open:
                CloseSuggestions();
                e.Handled = true;
                break;
        }
    }

    /// <summary>The suggestions for <paramref name="text"/>: those that contain it, the ones that start with it first, without the tags already added.</summary>
    internal IReadOnlyList<string> MatchesFor(string text)
    {
        if (Suggestions == null || string.IsNullOrWhiteSpace(text)) return [];
        var query = text.Trim();
        return Suggestions
            .Where(s => !string.IsNullOrEmpty(s))
            .Where(s => s.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Where(s => AllowDuplicates || !Tags.Any(t => string.Equals(t, s, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(s => s.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(s => s, StringComparer.CurrentCultureIgnoreCase)
            .Take(8)
            .ToList();
    }

    private void UpdateSuggestions()
    {
        _matches.Clear();
        foreach (var match in MatchesFor(_input?.Text ?? string.Empty)) _matches.Add(match);
        if (_popup != null) _popup.IsOpen = _matches.Count > 0 && _input is { IsKeyboardFocused: true };
        if (_list != null && _matches.Count > 0) _list.SelectedIndex = 0;
    }

    private void CommitSuggestion(string suggestion)
    {
        if (AddTag(suggestion)) _input?.Clear();
        CloseSuggestions();
        _input?.Focus();
    }

    private void CloseSuggestions()
    {
        if (_popup != null) _popup.IsOpen = false;
    }

    #endregion
}
