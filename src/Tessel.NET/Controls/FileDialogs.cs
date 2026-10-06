using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Tessel.NET.Helpers;

namespace Tessel.NET.Controls;

/// <summary>
/// Shared behavior of <see cref="FileChooserDialog"/> and <see cref="FileSaverDialog"/>: a themed, in-window dialog with a
/// <see cref="FileBrowser"/>, a name box and a file-type list. Both are shown with <c>ShowAsync</c>, which returns true when the user confirmed.
/// </summary>
public abstract class FileDialogBase
{
    private FileBrowser? _browser;
    private TextBox? _nameBox;
    private ComboBox? _filterBox;
    private TextBlock? _errorText;
    private ContentDialog? _dialog;
    private bool _accepted;
    private bool _syncingName;

    protected FileDialogBase(string title, string primaryButtonText)
    {
        Title = title;
        PrimaryButtonText = primaryButtonText;
    }

    public string Title { get; set; }

    /// <summary>The folder shown first. Defaults to the user's Documents folder.</summary>
    public string? InitialDirectory { get; set; }

    /// <summary>The file types, e.g. <c>new FileFilter("Images", "*.png;*.jpg")</c>. Empty means all files.</summary>
    public IList<FileFilter> Filters { get; } = [];

    /// <summary>Sets <see cref="Filters"/> from the classic <c>"Images|*.png;*.jpg|All files|*.*"</c> format.</summary>
    public string Filter
    {
        set
        {
            Filters.Clear();
            foreach (var filter in FileFilter.Parse(value)) Filters.Add(filter);
        }
    }

    /// <summary>Index of the selected file type in <see cref="Filters"/> (updated when the dialog closes).</summary>
    public int FilterIndex { get; set; }

    /// <summary>Lists hidden and system files.</summary>
    public bool ShowHiddenFiles { get; set; }

    /// <summary>How the files are listed: a table (default) or tiles with image previews. The user can switch in the dialog; the last choice is stored here when it closes.</summary>
    public FileBrowserView ViewMode { get; set; }

    /// <summary>Shows the "New folder" button (default true).</summary>
    public bool AllowNewFolder { get; set; } = true;

    public string PrimaryButtonText { get; set; }

    public string CloseButtonText { get; set; } = "Cancel";

    /// <summary>The folder that was open when the dialog closed.</summary>
    public string? CurrentDirectory { get; private set; }

    protected FileBrowser Browser => _browser!;

    protected TextBox NameBox => _nameBox!;

    protected FileFilter? SelectedFilter => _filterBox?.SelectedItem as FileFilter;

    protected virtual string NameHeader => "File name";

    protected virtual bool IsFolderPicker => false;

    protected virtual bool IsMultiselect => false;

    /// <summary>Called when the user confirms. Return true to accept the result and close the dialog.</summary>
    protected abstract Task<bool> TryAcceptAsync(Window? owner);

    /// <summary>Called when the selection in the browser changes by user action, to update the name box.</summary>
    protected abstract void OnSelectionChanged();

    protected virtual void OnShowing()
    {
    }

    /// <summary>Called after the browser, name box and file-type list have been created, before the dialog is shown.</summary>
    protected virtual void OnContentBuilt()
    {
    }

    protected void SetError(string? message)
    {
        if (_errorText == null) return;
        _errorText.Text = message ?? string.Empty;
        _errorText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Replaces the text of the name box without treating it as typing.</summary>
    protected void SetNameText(string text)
    {
        _syncingName = true;
        try
        {
            _nameBox!.Text = text;
            _nameBox.CaretIndex = text.Length;
        }
        finally
        {
            _syncingName = false;
        }
    }

    /// <summary>Shows the dialog over <paramref name="owner"/> (default: the active window). Returns true if the user confirmed.</summary>
    public async Task<bool> ShowAsync(Window? owner = null)
    {
        var window = OverlayHost.ResolveOwner(owner);
        _accepted = false;
        OnShowing();
        BuildContent();
        OnContentBuilt();

        _dialog = new ContentDialog
        {
            Title = Title,
            Content = BuildLayout(),
            PrimaryButtonText = PrimaryButtonText,
            CloseButtonText = CloseButtonText,
            DialogMaxWidth = 1000,
            Padding = new Thickness(24, 24, 24, 20),
        };
        _dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            await ConfirmAsync(window);
        };
        _browser!.EntryActivated += async (_, _) => await ConfirmAsync(window);

        var result = await _dialog.ShowAsync(window);
        FilterIndex = Math.Max(0, _filterBox?.SelectedIndex ?? 0);
        ViewMode = _browser.ViewMode;
        CurrentDirectory = _browser.CurrentDirectory;
        var accepted = result == ContentDialogResult.Primary && _accepted;
        _dialog = null;
        return accepted;
    }

    private bool _confirming;

    private async Task ConfirmAsync(Window? owner)
    {
        if (_confirming) return;
        _confirming = true;
        try
        {
            SetError(null);
            if (await TryAcceptAsync(owner))
            {
                _accepted = true;
                _dialog?.Hide(ContentDialogResult.Primary);
            }
        }
        finally
        {
            _confirming = false;
        }
    }

    private void BuildContent()
    {
        if (Filters.Count == 0) Filters.Add(FileFilter.AllFiles);

        _browser = new FileBrowser
        {
            Height = 360,
            Multiselect = IsMultiselect,
            FoldersOnly = IsFolderPicker,
            ShowHidden = ShowHiddenFiles,
            AllowNewFolder = AllowNewFolder,
            ViewMode = ViewMode,
            CurrentDirectory = InitialDirectory is { Length: > 0 } initial && Directory.Exists(initial)
                ? initial
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        _browser.SelectedEntriesChanged += (_, _) => OnSelectionChanged();

        _nameBox = new TextBox();
        ControlHelper.SetHeader(_nameBox, NameHeader);

        _filterBox = new ComboBox { ItemsSource = Filters.ToList(), DisplayMemberPath = nameof(FileFilter.DisplayName), MinWidth = 0 };
        ControlHelper.SetHeader(_filterBox, "File type");
        _filterBox.SelectedIndex = Math.Clamp(FilterIndex, 0, Filters.Count - 1);
        _browser.Filter = SelectedFilter;
        _filterBox.SelectionChanged += (_, _) => _browser.Filter = SelectedFilter;

        _errorText = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
        _errorText.SetResourceReference(TextBlock.ForegroundProperty, "Tessel.DangerBrush");
    }

    private UIElement BuildLayout()
    {
        var root = new Grid { Width = 840 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(_browser!);

        var fields = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        fields.ColumnDefinitions.Add(new ColumnDefinition());
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = IsFolderPicker ? new GridLength(0) : new GridLength(300) });
        fields.Children.Add(_nameBox!);
        if (!IsFolderPicker)
        {
            Grid.SetColumn(_filterBox!, 2);
            fields.Children.Add(_filterBox!);
        }
        Grid.SetRow(fields, 1);
        root.Children.Add(fields);

        Grid.SetRow(_errorText!, 2);
        root.Children.Add(_errorText!);
        return root;
    }

    /// <summary>True when the name box was edited by the user (not by a selection change).</summary>
    protected bool IsTyping => !_syncingName;

    /// <summary>Resolves a typed name against the current folder. Quoted names (<c>"a.txt" "b.txt"</c>) are split when several names are allowed.</summary>
    internal static IReadOnlyList<string> SplitNames(string text, bool allowMultiple)
    {
        text = text.Trim();
        if (text.Length == 0) return [];
        if (allowMultiple && text.Contains('"'))
        {
            return Regex.Matches(text, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        }
        return [text.Trim('"')];
    }

    internal static string ResolvePath(string directory, string name)
    {
        name = Environment.ExpandEnvironmentVariables(name);
        return Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.GetFullPath(Path.Combine(directory, name));
    }
}

/// <summary>
/// A themed "Open" dialog: pick one file, several files or a folder.
/// <code>
/// var dialog = new FileChooserDialog { Title = "Open image", Filter = "Images|*.png;*.jpg|All files|*.*", Multiselect = true };
/// if (await dialog.ShowAsync()) foreach (var path in dialog.FileNames) { ... }
/// </code>
/// </summary>
public sealed class FileChooserDialog : FileDialogBase
{
    private readonly List<string> _fileNames = [];

    public FileChooserDialog() : base("Open", "Open")
    {
    }

    /// <summary>Allows selecting several files with Ctrl/Shift-click (ignored when <see cref="SelectFolders"/>).</summary>
    public bool Multiselect { get; set; }

    /// <summary>Picks a folder instead of files.</summary>
    public bool SelectFolders { get; set; }

    /// <summary>Requires the chosen file to exist (default true).</summary>
    public bool CheckFileExists { get; set; } = true;

    /// <summary>The selected files or folder (full paths). Empty until <c>ShowAsync</c> returns true.</summary>
    public IReadOnlyList<string> FileNames => _fileNames;

    /// <summary>The first selected path, or null.</summary>
    public string? FileName => _fileNames.FirstOrDefault();

    protected override string NameHeader => SelectFolders ? "Folder" : "File name";

    protected override bool IsFolderPicker => SelectFolders;

    protected override bool IsMultiselect => Multiselect && !SelectFolders;

    protected override void OnShowing() => _fileNames.Clear();

    protected override void OnSelectionChanged()
    {
        var selected = SelectFolders ? Browser.SelectedEntries : Browser.SelectedFiles;
        SetError(null);
        if (selected.Count == 0) return;
        SetNameText(selected.Count == 1 ? selected[0].Name : string.Join(" ", selected.Select(e => $"\"{e.Name}\"")));
    }

    protected override async Task<bool> TryAcceptAsync(Window? owner)
    {
        await Task.CompletedTask;
        var directory = Browser.CurrentDirectory;
        var typed = NameBox.Text.Trim();
        var results = new List<string>();

        if (typed.Length == 0)
        {
            if (SelectFolders)
            {
                // No name: choose the selected folder, or the folder that is open.
                if (Browser.SelectedEntries.Count > 0) results.AddRange(Browser.SelectedEntries.Select(e => e.FullPath));
                else if (directory.Length > 0) results.Add(directory);
            }
            else
            {
                results.AddRange(Browser.SelectedFiles.Select(e => e.FullPath));
            }

            if (results.Count == 0)
            {
                SetError(SelectFolders ? "Select a folder." : "Select a file.");
                return false;
            }
        }
        else
        {
            foreach (var name in SplitNames(typed, Multiselect))
            {
                string path;
                try
                {
                    path = ResolvePath(directory, name);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    SetError($"“{name}” is not a valid name.");
                    return false;
                }

                if (Directory.Exists(path))
                {
                    if (!SelectFolders)
                    {
                        // Typing a folder opens it, like in the system dialogs.
                        Browser.Navigate(path);
                        NameBox.Clear();
                        return false;
                    }
                }
                else if (SelectFolders || (CheckFileExists && !File.Exists(path)))
                {
                    SetError(SelectFolders ? $"The folder “{name}” does not exist." : $"The file “{name}” was not found.");
                    return false;
                }
                results.Add(path);
            }
        }

        _fileNames.Clear();
        _fileNames.AddRange(results);
        return true;
    }
}

/// <summary>
/// A themed "Save as" dialog: choose a folder and a file name, with an overwrite confirmation.
/// <code>
/// var dialog = new FileSaverDialog { Title = "Export", FileName = "report", DefaultExtension = ".pdf", Filter = "PDF|*.pdf|All files|*.*" };
/// if (await dialog.ShowAsync()) File.WriteAllText(dialog.FileName!, ...);
/// </code>
/// </summary>
public sealed class FileSaverDialog : FileDialogBase
{
    public FileSaverDialog() : base("Save as", "Save")
    {
    }

    /// <summary>The suggested name before the dialog opens; the chosen full path afterwards.</summary>
    public string? FileName { get; set; }

    /// <summary>Appended when the typed name has no extension (e.g. <c>".txt"</c>). Falls back to the selected file type.</summary>
    public string? DefaultExtension { get; set; }

    /// <summary>Asks before replacing an existing file (default true).</summary>
    public bool OverwritePrompt { get; set; } = true;

    protected override void OnShowing()
    {
        // A full path as initial name also sets the initial folder.
        if (!string.IsNullOrWhiteSpace(FileName) && Path.IsPathRooted(FileName) && InitialDirectory == null)
        {
            var folder = Path.GetDirectoryName(FileName);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) InitialDirectory = folder;
        }
    }

    protected override void OnContentBuilt()
    {
        if (!string.IsNullOrWhiteSpace(FileName)) SetNameText(Path.GetFileName(FileName));
    }

    protected override void OnSelectionChanged()
    {
        SetError(null);
        if (Browser.SelectedFiles.Count == 1) SetNameText(Browser.SelectedFiles[0].Name);
    }

    /// <summary>Builds the final path from the typed name (adds the default extension). Exposed for tests.</summary>
    internal static string BuildPath(string directory, string typed, string? defaultExtension, FileFilter? filter)
    {
        var path = ResolvePath(directory, typed);
        if (Path.GetExtension(path).Length == 0)
        {
            var extension = defaultExtension ?? filter?.DefaultExtension;
            if (!string.IsNullOrEmpty(extension)) path += extension.StartsWith('.') ? extension : "." + extension;
        }
        return path;
    }

    protected override async Task<bool> TryAcceptAsync(Window? owner)
    {
        var typed = NameBox.Text.Trim();
        if (typed.Length == 0)
        {
            SetError("Enter a file name.");
            return false;
        }

        string path;
        try
        {
            path = ResolvePath(Browser.CurrentDirectory, typed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            SetError($"“{typed}” is not a valid file name.");
            return false;
        }

        if (Directory.Exists(path))
        {
            // Typing a folder opens it instead of saving.
            Browser.Navigate(path);
            NameBox.Clear();
            return false;
        }

        path = BuildPath(Browser.CurrentDirectory, typed, DefaultExtension, SelectedFilter);
        var directory = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            SetError("The folder does not exist.");
            return false;
        }
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            SetError("The file name contains characters that are not allowed.");
            return false;
        }

        if (OverwritePrompt && File.Exists(path))
        {
            var answer = await MessageDialog.ShowAsync(
                "Replace file?",
                $"“{name}” already exists in this location. Do you want to replace it?",
                MessageDialogButtons.YesNo,
                MessageDialogIcon.Warning,
                owner,
                destructive: true);
            if (answer != MessageDialogResult.Yes) return false;
        }

        FileName = path;
        return true;
    }
}
