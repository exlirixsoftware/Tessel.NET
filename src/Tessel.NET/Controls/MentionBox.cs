using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Tessel.NET.Controls;

/// <summary>
/// A multi-line text box that offers people while you type: write "@" (<see cref="Trigger"/>) and a few letters, pick a name with the arrow
/// keys and Enter (or a click), and "@Name " is put into the text. Use it for comments, chat and notes. <see cref="GetMentions"/> returns
/// the names that are mentioned in the text.
/// </summary>
[TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_List", Type = typeof(ListBox))]
public class MentionBox : Control
{
    private TextBox? _text;
    private Popup? _popup;
    private ListBox? _list;
    private int _tokenStart = -1;
    private readonly ObservableCollection<string> _matches = [];

    static MentionBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MentionBox), new FrameworkPropertyMetadata(typeof(MentionBox)));
        FocusableProperty.OverrideMetadata(typeof(MentionBox), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(MentionBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((MentionBox)d).OnTextPropertyChanged((string)e.NewValue)));

    public static readonly DependencyProperty CandidatesProperty = DependencyProperty.Register(
        nameof(Candidates), typeof(IEnumerable), typeof(MentionBox), new PropertyMetadata(null));

    public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
        nameof(DisplayMemberPath), typeof(string), typeof(MentionBox), new PropertyMetadata(null));

    public static readonly DependencyProperty TriggerProperty = DependencyProperty.Register(
        nameof(Trigger), typeof(string), typeof(MentionBox), new PropertyMetadata("@"));

    public static readonly DependencyProperty MaxSuggestionsProperty = DependencyProperty.Register(
        nameof(MaxSuggestions), typeof(int), typeof(MentionBox), new PropertyMetadata(6, null, (_, v) => Math.Max(1, (int)v)));

    public static readonly DependencyProperty AcceptsReturnProperty = DependencyProperty.Register(
        nameof(AcceptsReturn), typeof(bool), typeof(MentionBox), new PropertyMetadata(true));

    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
        nameof(TextWrapping), typeof(TextWrapping), typeof(MentionBox), new PropertyMetadata(TextWrapping.Wrap));

    /// <summary>The text, with the mentions in it.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The people that can be mentioned: names, or objects read with <see cref="DisplayMemberPath"/>.</summary>
    public IEnumerable? Candidates
    {
        get => (IEnumerable?)GetValue(CandidatesProperty);
        set => SetValue(CandidatesProperty, value);
    }

    public string? DisplayMemberPath
    {
        get => (string?)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    /// <summary>The character that starts a mention ("@").</summary>
    public string Trigger
    {
        get => (string)GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    public int MaxSuggestions
    {
        get => (int)GetValue(MaxSuggestionsProperty);
        set => SetValue(MaxSuggestionsProperty, value);
    }

    public bool AcceptsReturn
    {
        get => (bool)GetValue(AcceptsReturnProperty);
        set => SetValue(AcceptsReturnProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    /// <summary>Raised when a name was inserted into the text.</summary>
    public event EventHandler<string>? MentionInserted;

    /// <summary>The inner text box (for the caret, selection and focus).</summary>
    public TextBox? TextBox => _text;

    private IEnumerable<string> Names()
    {
        if (Candidates == null) yield break;
        foreach (var item in Candidates)
        {
            if (item == null) continue;
            var name = item as string ?? (string.IsNullOrEmpty(DisplayMemberPath) ? null : item.GetType().GetProperty(DisplayMemberPath)?.GetValue(item)?.ToString()) ?? item.ToString();
            if (!string.IsNullOrWhiteSpace(name)) yield return name!;
        }
    }

    /// <summary>The candidates that are mentioned in the text ("@Ada" mentions Ada), in the order they are written.</summary>
    public IReadOnlyList<string> GetMentions()
    {
        var text = Text ?? string.Empty;
        var found = new List<(int At, string Name)>();
        foreach (var name in Names().Distinct(StringComparer.CurrentCultureIgnoreCase))
        {
            var token = Trigger + name;
            var at = 0;
            while ((at = text.IndexOf(token, at, StringComparison.CurrentCultureIgnoreCase)) >= 0)
            {
                var end = at + token.Length;
                var startsWord = at == 0 || char.IsWhiteSpace(text[at - 1]);
                var endsWord = end >= text.Length || !char.IsLetterOrDigit(text[end]);
                if (startsWord && endsWord)
                {
                    found.Add((at, name));
                    break;
                }
                at = end;
            }
        }
        return found.OrderBy(f => f.At).Select(f => f.Name).ToList();
    }

    #region Template

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _text = GetTemplateChild("PART_TextBox") as TextBox;
        _popup = GetTemplateChild("PART_Popup") as Popup;
        _list = GetTemplateChild("PART_List") as ListBox;

        if (_list != null)
        {
            _list.ItemsSource = _matches;
            _list.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_list, source) is ListBoxItem { DataContext: string name })
                {
                    Insert(name);
                    e.Handled = true;
                }
            };
        }
        if (_text != null)
        {
            _text.Text = Text;
            _text.TextChanged += (_, _) =>
            {
                if (Text != _text.Text) Text = _text.Text;
                UpdateSuggestions();
            };
            _text.SelectionChanged += (_, _) => UpdateSuggestions();
            _text.PreviewKeyDown += OnKeyDownInText;
            _text.LostKeyboardFocus += (_, _) =>
            {
                if (_list?.IsMouseOver != true) Close();
            };
        }
    }

    private void OnTextPropertyChanged(string text)
    {
        if (_text != null && _text.Text != text) _text.Text = text ?? string.Empty;
    }

    #endregion

    #region Mentions

    /// <summary>The start of the mention the caret is in, and what has been typed after the trigger; null when the caret is not in one.</summary>
    internal static (int Start, string Query)? TokenAt(string text, int caret, string trigger)
    {
        if (string.IsNullOrEmpty(trigger) || caret < 0 || caret > text.Length) return null;
        for (var i = caret - 1; i >= 0; i--)
        {
            if (string.CompareOrdinal(text, i, trigger, 0, trigger.Length) == 0)
            {
                if (i > 0 && !char.IsWhiteSpace(text[i - 1])) return null;
                return (i, text.Substring(i + trigger.Length, caret - i - trigger.Length));
            }
            if (char.IsWhiteSpace(text[i])) return null;
        }
        return null;
    }

    internal IReadOnlyList<string> MatchesFor(string query)
    {
        return Names()
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Where(n => n.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(n => n.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxSuggestions)
            .ToList();
    }

    private void UpdateSuggestions()
    {
        if (_text == null || _popup == null) return;
        var token = TokenAt(_text.Text, _text.CaretIndex, Trigger);
        if (token == null || !_text.IsKeyboardFocused)
        {
            Close();
            return;
        }

        _tokenStart = token.Value.Start;
        var matches = MatchesFor(token.Value.Query);
        _matches.Clear();
        foreach (var match in matches) _matches.Add(match);
        if (_matches.Count == 0)
        {
            Close();
            return;
        }

        _popup.PlacementTarget = _text;
        _popup.Placement = PlacementMode.Bottom;
        _popup.PlacementRectangle = _text.GetRectFromCharacterIndex(_tokenStart);
        _popup.IsOpen = true;
        if (_list != null) _list.SelectedIndex = 0;
    }

    private void Close()
    {
        if (_popup != null) _popup.IsOpen = false;
        _tokenStart = -1;
    }

    /// <summary>Puts "@name " in place of the mention that is being typed.</summary>
    internal void Insert(string name)
    {
        if (_text == null) return;
        var caret = _text.CaretIndex;
        if (TokenAt(_text.Text, caret, Trigger) is not { } token) return;
        var before = _text.Text[..token.Start];
        var after = caret <= _text.Text.Length ? _text.Text[caret..] : string.Empty;
        var inserted = Trigger + name + " ";
        Close();
        _text.Text = before + inserted + after;
        _text.CaretIndex = before.Length + inserted.Length;
        _text.Focus();
        MentionInserted?.Invoke(this, name);
    }

    private void OnKeyDownInText(object sender, KeyEventArgs e)
    {
        if (_popup is not { IsOpen: true } || _list == null || _matches.Count == 0) return;
        switch (e.Key)
        {
            case Key.Down:
                _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _matches.Count - 1);
                e.Handled = true;
                break;
            case Key.Up:
                _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Tab:
                if (_list.SelectedItem is string name) Insert(name);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    #endregion
}
