using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Tessel.NET.Helpers;

namespace Tessel.NET.Controls;

/// <summary>An action that can be found and run in a <see cref="CommandPalette"/>.</summary>
public sealed class PaletteCommand
{
    public PaletteCommand(string title, Action execute)
    {
        Title = title;
        Execute = execute;
    }

    /// <summary>The name that is searched and shown.</summary>
    public string Title { get; }

    /// <summary>A line of explanation under the title.</summary>
    public string? Description { get; init; }

    /// <summary>The shortcut shown on the right (text only; the palette does not handle it).</summary>
    public string? Shortcut { get; init; }

    /// <summary>A group name shown small above the title ("File", "View").</summary>
    public string? Category { get; init; }

    /// <summary>An icon-font character shown before the title.</summary>
    public string? Glyph { get; init; }

    /// <summary>More words that find the command ("quit exit close" for "Exit").</summary>
    public string? Keywords { get; init; }

    public bool IsEnabled { get; init; } = true;

    public Action Execute { get; }
}

/// <summary>What the list of a <see cref="CommandPalette"/> shows for one match.</summary>
public sealed class PaletteItem(PaletteCommand command, int score)
{
    public PaletteCommand Command { get; } = command;

    public int Score { get; } = score;

    public string Title => Command.Title;

    public string? Description => Command.Description;

    public string? Shortcut => Command.Shortcut;

    public string? Category => Command.Category;

    public string? Glyph => Command.Glyph;

    public bool IsEnabled => Command.IsEnabled;
}

/// <summary>
/// The "go to anything" box of an editor: press Ctrl+K, type a few letters of a command, press Enter. Typing narrows the list (every word
/// must match the title, category or keywords, letters may be skipped: "svas" finds "Save as"); the arrow keys choose, Enter runs, Escape
/// or a click outside closes.
/// <code>CommandPalette.Attach(this, commands);   // Ctrl+K
/// CommandPalette.Show(this, commands);   // or open it yourself</code>
/// </summary>
[TemplatePart(Name = "PART_Search", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_List", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_Backdrop", Type = typeof(FrameworkElement))]
public class CommandPalette : Control
{
    private static readonly Dictionary<Window, (KeyEventHandler Handler, CommandPalette? Open)> Attached = [];

    private TextBox? _search;
    private ListBox? _list;
    private Adorner? _adorner;
    private IInputElement? _previousFocus;
    private bool _closing;

    static CommandPalette()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CommandPalette), new FrameworkPropertyMetadata(typeof(CommandPalette)));
        FocusableProperty.OverrideMetadata(typeof(CommandPalette), new FrameworkPropertyMetadata(false));
    }

    public CommandPalette(IEnumerable<PaletteCommand> commands)
    {
        Commands = commands.ToList();
        Results = new ObservableCollection<PaletteItem>(Rank(Commands, string.Empty));
    }

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(CommandPalette), new PropertyMetadata("Type a command..."));

    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(
        nameof(Query), typeof(string), typeof(CommandPalette), new PropertyMetadata(string.Empty, (d, e) => ((CommandPalette)d).OnQueryChanged((string)e.NewValue)));

    public static readonly DependencyProperty MaxResultsProperty = DependencyProperty.Register(
        nameof(MaxResults), typeof(int), typeof(CommandPalette), new PropertyMetadata(12, (d, _) => ((CommandPalette)d).Refresh(), (_, v) => Math.Max(1, (int)v)));

    /// <summary>The hint in the search box.</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>The text that is searched for.</summary>
    public string Query
    {
        get => (string)GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    /// <summary>How many matches are listed at most.</summary>
    public int MaxResults
    {
        get => (int)GetValue(MaxResultsProperty);
        set => SetValue(MaxResultsProperty, value);
    }

    /// <summary>All the commands of this palette.</summary>
    public IReadOnlyList<PaletteCommand> Commands { get; }

    /// <summary>The commands that match <see cref="Query"/>, best first.</summary>
    public ObservableCollection<PaletteItem> Results { get; }

    /// <summary>Raised after the palette closed; the command is null when none was chosen.</summary>
    public event EventHandler<PaletteCommand?>? Closed;

    #region Search

    /// <summary>
    /// The matching commands, best first. Every word of the query must match: a title that starts with it scores highest, then the start of a
    /// word, then any part, and finally letters in order with gaps ("svas" in "Save as"). The category and the keywords count less.
    /// </summary>
    public static IReadOnlyList<PaletteItem> Rank(IEnumerable<PaletteCommand> commands, string query)
    {
        var words = (query ?? string.Empty).Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var items = new List<PaletteItem>();
        var index = 0;
        foreach (var command in commands)
        {
            index++;
            var total = 0;
            var matches = true;
            foreach (var word in words)
            {
                var score = Math.Max(Score(command.Title, word), Math.Max(Score(command.Category, word) / 2, Score(command.Keywords, word) / 2));
                if (score == 0)
                {
                    matches = false;
                    break;
                }
                total += score;
            }
            if (matches) items.Add(new PaletteItem(command, words.Length == 0 ? 1000 - index : total));
        }
        return words.Length == 0
            ? items
            : items.OrderByDescending(item => item.Score).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static int Score(string? text, string word)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var at = text.IndexOf(word, StringComparison.CurrentCultureIgnoreCase);
        if (at == 0) return 100;
        if (at > 0)
        {
            return char.IsWhiteSpace(text[at - 1]) || text[at - 1] is '-' or '_' or '.' or '/' ? 80 : 60;
        }

        // letters in order, with gaps
        var position = 0;
        foreach (var ch in word)
        {
            position = text.IndexOf(ch.ToString(), position, StringComparison.CurrentCultureIgnoreCase);
            if (position < 0) return 0;
            position++;
        }
        return 30;
    }

    private void OnQueryChanged(string query)
    {
        if (_search != null && _search.Text != query) _search.Text = query;
        Refresh();
    }

    private void Refresh()
    {
        var ranked = Rank(Commands, Query).Take(MaxResults).ToList();
        Results.Clear();
        foreach (var item in ranked) Results.Add(item);
        if (_list != null && Results.Count > 0)
        {
            var first = Results.ToList().FindIndex(r => r.IsEnabled);
            _list.SelectedIndex = Math.Max(first, 0);
        }
    }

    #endregion

    #region Showing

    /// <summary>Opens a palette over the window (the active window when null) and focuses its search box.</summary>
    public static CommandPalette? Show(Window? owner, IEnumerable<PaletteCommand> commands, string? placeholder = null)
    {
        var window = OverlayHost.ResolveOwner(owner);
        if (window == null) return null;

        var palette = new CommandPalette(commands);
        if (placeholder != null) palette.Placeholder = placeholder;
        palette._previousFocus = Keyboard.FocusedElement;
        palette._adorner = OverlayHost.Show(window, palette);
        if (palette._adorner == null) return null;

        if (Attached.TryGetValue(window, out var entry)) Attached[window] = (entry.Handler, palette);
        palette.Closed += (_, _) =>
        {
            if (Attached.TryGetValue(window, out var current)) Attached[window] = (current.Handler, null);
        };
        palette.Dispatcher.BeginInvoke(() => palette._search?.Focus(), System.Windows.Threading.DispatcherPriority.Loaded);
        return palette;
    }

    /// <summary>Makes <paramref name="key"/> with <paramref name="modifiers"/> (Ctrl+K) open a palette of <paramref name="commands"/> in the window.</summary>
    public static void Attach(Window window, IEnumerable<PaletteCommand> commands, Key key = Key.K, ModifierKeys modifiers = ModifierKeys.Control)
    {
        Detach(window);
        var list = commands.ToList();
        KeyEventHandler handler = (_, e) =>
        {
            if (e.Key != key || Keyboard.Modifiers != modifiers) return;
            if (Attached.TryGetValue(window, out var entry) && entry.Open != null) return;
            if (Show(window, list) != null) e.Handled = true;
        };
        window.PreviewKeyDown += handler;
        Attached[window] = (handler, null);
    }

    public static void Detach(Window window)
    {
        if (!Attached.Remove(window, out var entry)) return;
        window.PreviewKeyDown -= entry.Handler;
        entry.Open?.Close();
    }

    /// <summary>Closes the palette without running anything.</summary>
    public void Close() => Finish(null);

    /// <summary>Closes the palette and runs the command.</summary>
    public void Run(PaletteCommand command)
    {
        if (!command.IsEnabled) return;
        Finish(command);
    }

    private void Finish(PaletteCommand? command)
    {
        if (_closing) return;
        _closing = true;
        if (_adorner != null) OverlayHost.Close(_adorner);
        _adorner = null;
        if (_previousFocus is UIElement previous) previous.Focus();
        Closed?.Invoke(this, command);
        if (command != null) Dispatcher.BeginInvoke(command.Execute);
    }

    #endregion

    #region Template and input

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _search = GetTemplateChild("PART_Search") as TextBox;
        _list = GetTemplateChild("PART_List") as ListBox;
        var backdrop = GetTemplateChild("PART_Backdrop") as FrameworkElement;

        if (_list != null) _list.ItemsSource = Results;
        if (backdrop != null) backdrop.MouseLeftButtonDown += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, backdrop))
            {
                Close();
                e.Handled = true;
            }
        };
        if (_search != null)
        {
            _search.Text = Query;
            _search.TextChanged += (_, _) => Query = _search.Text;
            _search.PreviewKeyDown += OnSearchKeyDown;
        }
        if (_list != null)
        {
            _list.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_list, source) is ListBoxItem { DataContext: PaletteItem item })
                {
                    Run(item.Command);
                    e.Handled = true;
                }
            };
        }
        Refresh();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Down:
                Move(1);
                e.Handled = true;
                break;
            case Key.Up:
                Move(-1);
                e.Handled = true;
                break;
            case Key.PageDown:
                Move(5);
                e.Handled = true;
                break;
            case Key.PageUp:
                Move(-5);
                e.Handled = true;
                break;
            case Key.Enter:
                if (_list?.SelectedItem is PaletteItem item) Run(item.Command);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Moves the selection by <paramref name="delta"/> items, skipping disabled commands.</summary>
    internal void Move(int delta)
    {
        if (_list == null || Results.Count == 0) return;
        var index = _list.SelectedIndex;
        var step = delta > 0 ? 1 : -1;
        for (var remaining = Math.Abs(delta); remaining > 0; remaining--)
        {
            var next = index + step;
            while (next >= 0 && next < Results.Count && !Results[next].IsEnabled) next += step;
            if (next < 0 || next >= Results.Count) break;
            index = next;
        }
        _list.SelectedIndex = index;
        _list.ScrollIntoView(_list.SelectedItem);
    }

    #endregion
}
