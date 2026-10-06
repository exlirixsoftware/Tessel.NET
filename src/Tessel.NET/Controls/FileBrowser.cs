using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Tessel.NET.Helpers;

namespace Tessel.NET.Controls;

/// <summary>
/// Browses the file system: places, a clickable/editable path, back/forward/up navigation and a sortable file list.
/// It is the building block of <see cref="FileChooserDialog"/> and <see cref="FileSaverDialog"/> but can be used on its own.
/// </summary>
[TemplatePart(Name = "PART_BackButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_ForwardButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_UpButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_RefreshButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_NewFolderButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_PathHost", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_PathBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Breadcrumb", Type = typeof(BreadcrumbBar))]
[TemplatePart(Name = "PART_BreadcrumbScroller", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_PlacesList", Type = typeof(ListBox))]
[TemplatePart(Name = "PART_FileList", Type = typeof(ListView))]
[TemplatePart(Name = "PART_SearchBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_ViewSwitch", Type = typeof(Selector))]
public class FileBrowser : Control
{
    private ButtonBase? _back;
    private ButtonBase? _forward;
    private ButtonBase? _up;
    private ButtonBase? _refresh;
    private ButtonBase? _newFolder;
    private FrameworkElement? _pathHost;
    private TextBox? _pathBox;
    private BreadcrumbBar? _breadcrumb;
    private ScrollViewer? _crumbScroller;
    private ListBox? _places;
    private ListView? _list;
    private TextBox? _searchBox;
    private Selector? _viewSwitch;
    private readonly System.Windows.Threading.DispatcherTimer _searchTimer;
    private bool _syncingSearch;
    private bool _syncingView;

    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private string? _current;
    private bool _syncingDirectory;
    private bool _syncingPlaces;
    private CancellationTokenSource? _loadCts;
    private string _sortColumn = "Name";
    private bool _sortDescending;

    static FileBrowser()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FileBrowser), new FrameworkPropertyMetadata(typeof(FileBrowser)));
        FocusableProperty.OverrideMetadata(typeof(FileBrowser), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(FileBrowser), new FrameworkPropertyMetadata(false));
    }

    public FileBrowser()
    {
        _searchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            Refresh();
        };
        Places = FilePlace.CreateDefaults();
        Entries = [];
        PathSegments = [];
    }

    #region Dependency properties

    /// <summary>The folder being shown (empty = the list of drives). Setting it navigates.</summary>
    public static readonly DependencyProperty CurrentDirectoryProperty = DependencyProperty.Register(
        nameof(CurrentDirectory), typeof(string), typeof(FileBrowser),
        new FrameworkPropertyMetadata(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentDirectoryChanged));

    public string CurrentDirectory
    {
        get => (string)GetValue(CurrentDirectoryProperty);
        set => SetValue(CurrentDirectoryProperty, value);
    }

    public static readonly DependencyProperty MultiselectProperty = DependencyProperty.Register(
        nameof(Multiselect), typeof(bool), typeof(FileBrowser), new PropertyMetadata(false, (d, _) => ((FileBrowser)d).UpdateSelectionMode()));

    public bool Multiselect
    {
        get => (bool)GetValue(MultiselectProperty);
        set => SetValue(MultiselectProperty, value);
    }

    /// <summary>Shows folders only (to pick a folder).</summary>
    public static readonly DependencyProperty FoldersOnlyProperty = DependencyProperty.Register(
        nameof(FoldersOnly), typeof(bool), typeof(FileBrowser), new PropertyMetadata(false, (d, _) => ((FileBrowser)d).Refresh()));

    public bool FoldersOnly
    {
        get => (bool)GetValue(FoldersOnlyProperty);
        set => SetValue(FoldersOnlyProperty, value);
    }

    public static readonly DependencyProperty ShowHiddenProperty = DependencyProperty.Register(
        nameof(ShowHidden), typeof(bool), typeof(FileBrowser), new PropertyMetadata(false, (d, _) => ((FileBrowser)d).Refresh()));

    /// <summary>Shows hidden and system files.</summary>
    public bool ShowHidden
    {
        get => (bool)GetValue(ShowHiddenProperty);
        set => SetValue(ShowHiddenProperty, value);
    }

    /// <summary>Only files matching the filter are listed (folders always are). Null shows all files.</summary>
    public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(
        nameof(Filter), typeof(FileFilter), typeof(FileBrowser), new PropertyMetadata(null, (d, _) => ((FileBrowser)d).Refresh()));

    public FileFilter? Filter
    {
        get => (FileFilter?)GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    /// <summary>Shows the "New folder" button.</summary>
    public static readonly DependencyProperty AllowNewFolderProperty = DependencyProperty.Register(
        nameof(AllowNewFolder), typeof(bool), typeof(FileBrowser), new PropertyMetadata(true));

    public bool AllowNewFolder
    {
        get => (bool)GetValue(AllowNewFolderProperty);
        set => SetValue(AllowNewFolderProperty, value);
    }

    private static readonly DependencyPropertyKey EntriesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Entries), typeof(IReadOnlyList<FileEntry>), typeof(FileBrowser), new PropertyMetadata(null));

    public static readonly DependencyProperty EntriesProperty = EntriesPropertyKey.DependencyProperty;

    /// <summary>The entries of the current folder (after filtering and sorting).</summary>
    public IReadOnlyList<FileEntry> Entries
    {
        get => (IReadOnlyList<FileEntry>)GetValue(EntriesProperty);
        private set => SetValue(EntriesPropertyKey, value);
    }

    private static readonly DependencyPropertyKey PathSegmentsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(PathSegments), typeof(IReadOnlyList<PathSegment>), typeof(FileBrowser), new PropertyMetadata(null));

    public static readonly DependencyProperty PathSegmentsProperty = PathSegmentsPropertyKey.DependencyProperty;

    public IReadOnlyList<PathSegment> PathSegments
    {
        get => (IReadOnlyList<PathSegment>)GetValue(PathSegmentsProperty);
        private set => SetValue(PathSegmentsPropertyKey, value);
    }

    private static readonly DependencyPropertyKey PlacesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Places), typeof(IReadOnlyList<FilePlace>), typeof(FileBrowser), new PropertyMetadata(null));

    public static readonly DependencyProperty PlacesProperty = PlacesPropertyKey.DependencyProperty;

    public IReadOnlyList<FilePlace> Places
    {
        get => (IReadOnlyList<FilePlace>)GetValue(PlacesProperty);
        private set => SetValue(PlacesPropertyKey, value);
    }

    private static readonly DependencyPropertyKey StatusTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(StatusText), typeof(string), typeof(FileBrowser), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusTextProperty = StatusTextPropertyKey.DependencyProperty;

    /// <summary>"This folder is empty", an access error, ... (empty when the list has entries).</summary>
    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        private set => SetValue(StatusTextPropertyKey, value);
    }

    private static readonly DependencyPropertyKey IsLoadingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsLoading), typeof(bool), typeof(FileBrowser), new PropertyMetadata(false));

    public static readonly DependencyProperty IsLoadingProperty = IsLoadingPropertyKey.DependencyProperty;

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        private set => SetValue(IsLoadingPropertyKey, value);
    }

    private static readonly DependencyPropertyKey CanGoBackPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CanGoBack), typeof(bool), typeof(FileBrowser), new PropertyMetadata(false));

    public static readonly DependencyProperty CanGoBackProperty = CanGoBackPropertyKey.DependencyProperty;

    public bool CanGoBack
    {
        get => (bool)GetValue(CanGoBackProperty);
        private set => SetValue(CanGoBackPropertyKey, value);
    }

    private static readonly DependencyPropertyKey CanGoForwardPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CanGoForward), typeof(bool), typeof(FileBrowser), new PropertyMetadata(false));

    public static readonly DependencyProperty CanGoForwardProperty = CanGoForwardPropertyKey.DependencyProperty;

    public bool CanGoForward
    {
        get => (bool)GetValue(CanGoForwardProperty);
        private set => SetValue(CanGoForwardPropertyKey, value);
    }

    private static readonly DependencyPropertyKey CanGoUpPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CanGoUp), typeof(bool), typeof(FileBrowser), new PropertyMetadata(false));

    public static readonly DependencyProperty CanGoUpProperty = CanGoUpPropertyKey.DependencyProperty;

    public bool CanGoUp
    {
        get => (bool)GetValue(CanGoUpProperty);
        private set => SetValue(CanGoUpPropertyKey, value);
    }

    /// <summary>True while the path is being edited as text.</summary>
    public static readonly DependencyProperty IsEditingPathProperty = DependencyProperty.Register(
        nameof(IsEditingPath), typeof(bool), typeof(FileBrowser), new PropertyMetadata(false));

    public bool IsEditingPath
    {
        get => (bool)GetValue(IsEditingPathProperty);
        private set => SetValue(IsEditingPathProperty, value);
    }

    /// <summary>
    /// Text typed in the search box. A non-empty value lists the entries of the current folder and its subfolders
    /// whose name contains it. Navigating to another folder clears it.
    /// </summary>
    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        nameof(SearchText), typeof(string), typeof(FileBrowser),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged, (_, v) => v ?? string.Empty));

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <summary>Details (table) or Thumbnails (tiles with image previews).</summary>
    public static readonly DependencyProperty ViewModeProperty = DependencyProperty.Register(
        nameof(ViewMode), typeof(FileBrowserView), typeof(FileBrowser),
        new FrameworkPropertyMetadata(FileBrowserView.Details, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnViewModeChanged));

    public FileBrowserView ViewMode
    {
        get => (FileBrowserView)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    private static readonly DependencyPropertyKey SummaryTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SummaryText), typeof(string), typeof(FileBrowser), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SummaryTextProperty = SummaryTextPropertyKey.DependencyProperty;

    /// <summary>"12 items" or "5 results for “x”".</summary>
    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        private set => SetValue(SummaryTextPropertyKey, value);
    }

    #endregion

    /// <summary>Raised after a folder has been listed.</summary>
    public event EventHandler? DirectoryChanged;

    /// <summary>Raised when the selection in the list changes.</summary>
    public event EventHandler? SelectedEntriesChanged;

    /// <summary>Raised when a file is double-clicked or Enter is pressed on it (folders are opened instead).</summary>
    public event EventHandler<FileEntry>? EntryActivated;

    /// <summary>Completes when the folder that is currently loading has been listed (used by tests and tools).</summary>
    internal Task LoadTask { get; private set; } = Task.CompletedTask;

    /// <summary>The selected entries (files and folders; only folders when <see cref="FoldersOnly"/>).</summary>
    public IReadOnlyList<FileEntry> SelectedEntries => _list?.SelectedItems.OfType<FileEntry>().ToList() ?? [];

    /// <summary>The selected files (no folders).</summary>
    public IReadOnlyList<FileEntry> SelectedFiles => SelectedEntries.Where(e => !e.IsDirectory).ToList();

    public override void OnApplyTemplate()
    {
        Detach();
        base.OnApplyTemplate();

        _back = GetTemplateChild("PART_BackButton") as ButtonBase;
        _forward = GetTemplateChild("PART_ForwardButton") as ButtonBase;
        _up = GetTemplateChild("PART_UpButton") as ButtonBase;
        _refresh = GetTemplateChild("PART_RefreshButton") as ButtonBase;
        _newFolder = GetTemplateChild("PART_NewFolderButton") as ButtonBase;
        _pathHost = GetTemplateChild("PART_PathHost") as FrameworkElement;
        _pathBox = GetTemplateChild("PART_PathBox") as TextBox;
        _breadcrumb = GetTemplateChild("PART_Breadcrumb") as BreadcrumbBar;
        _crumbScroller = GetTemplateChild("PART_BreadcrumbScroller") as ScrollViewer;
        _places = GetTemplateChild("PART_PlacesList") as ListBox;
        _list = GetTemplateChild("PART_FileList") as ListView;
        _searchBox = GetTemplateChild("PART_SearchBox") as TextBox;
        _viewSwitch = GetTemplateChild("PART_ViewSwitch") as Selector;

        if (_back != null) _back.Click += OnBackClick;
        if (_forward != null) _forward.Click += OnForwardClick;
        if (_up != null) _up.Click += OnUpClick;
        if (_refresh != null) _refresh.Click += OnRefreshClick;
        if (_newFolder != null) _newFolder.Click += OnNewFolderClick;
        if (_pathHost != null) _pathHost.MouseLeftButtonDown += OnPathHostMouseDown;
        if (_pathBox != null)
        {
            _pathBox.PreviewKeyDown += OnPathBoxKeyDown;
            _pathBox.LostKeyboardFocus += OnPathBoxLostFocus;
        }
        if (_breadcrumb != null) _breadcrumb.ItemClicked += OnCrumbClicked;
        if (_searchBox != null) _searchBox.PreviewKeyDown += OnSearchKeyDown;
        if (_viewSwitch != null)
        {
            _viewSwitch.SelectionChanged += OnViewSwitchChanged;
            SyncViewSwitch();
        }
        if (_places != null) _places.SelectionChanged += OnPlaceSelected;
        if (_list != null)
        {
            _list.SelectionChanged += OnListSelectionChanged;
            _list.MouseDoubleClick += OnListDoubleClick;
            _list.PreviewKeyDown += OnListKeyDown;
            _list.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnHeaderClick));
        }

        UpdateSelectionMode();
        if (_current == null) NavigateCore(CurrentDirectory, recordHistory: false);
        else UpdateNavigationState();
    }

    private void Detach()
    {
        if (_back != null) _back.Click -= OnBackClick;
        if (_forward != null) _forward.Click -= OnForwardClick;
        if (_up != null) _up.Click -= OnUpClick;
        if (_refresh != null) _refresh.Click -= OnRefreshClick;
        if (_newFolder != null) _newFolder.Click -= OnNewFolderClick;
        if (_pathHost != null) _pathHost.MouseLeftButtonDown -= OnPathHostMouseDown;
        if (_pathBox != null)
        {
            _pathBox.PreviewKeyDown -= OnPathBoxKeyDown;
            _pathBox.LostKeyboardFocus -= OnPathBoxLostFocus;
        }
        if (_breadcrumb != null) _breadcrumb.ItemClicked -= OnCrumbClicked;
        if (_searchBox != null) _searchBox.PreviewKeyDown -= OnSearchKeyDown;
        if (_viewSwitch != null) _viewSwitch.SelectionChanged -= OnViewSwitchChanged;
        if (_places != null) _places.SelectionChanged -= OnPlaceSelected;
        if (_list != null)
        {
            _list.SelectionChanged -= OnListSelectionChanged;
            _list.MouseDoubleClick -= OnListDoubleClick;
            _list.PreviewKeyDown -= OnListKeyDown;
            _list.RemoveHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnHeaderClick));
        }
    }

    #region Navigation

    /// <summary>Navigates to a folder (or to the folder of a file, selecting it). Returns false if the path does not exist.</summary>
    public bool Navigate(string? path) => NavigateCore(path, recordHistory: true);

    public void GoUp()
    {
        if (_current is not { Length: > 0 } current) return;
        var parent = Directory.GetParent(current)?.FullName ?? string.Empty;
        NavigateCore(parent, recordHistory: true, selectName: Path.GetFileName(current.TrimEnd(Path.DirectorySeparatorChar)));
    }

    public void GoBack()
    {
        if (_backStack.Count == 0 || _current == null) return;
        _forwardStack.Push(_current);
        NavigateCore(_backStack.Pop(), recordHistory: false);
    }

    public void GoForward()
    {
        if (_forwardStack.Count == 0 || _current == null) return;
        _backStack.Push(_current);
        NavigateCore(_forwardStack.Pop(), recordHistory: false);
    }

    public void Refresh()
    {
        if (_current != null) LoadTask = LoadAsync(_current, null);
    }

    private bool NavigateCore(string? path, bool recordHistory, string? selectName = null)
    {
        path = Normalize(path);
        if (path.Length > 0)
        {
            if (File.Exists(path))
            {
                selectName = Path.GetFileName(path);
                path = Path.GetDirectoryName(path) ?? string.Empty;
            }
            if (path.Length > 0 && !Directory.Exists(path))
            {
                StatusText = $"Cannot find “{path}”.";
                return false;
            }
        }

        if (recordHistory && _current != null && !string.Equals(_current, path, StringComparison.OrdinalIgnoreCase))
        {
            _backStack.Push(_current);
            _forwardStack.Clear();
        }
        _current = path;
        ClearSearch();

        _syncingDirectory = true;
        try
        {
            SetCurrentValue(CurrentDirectoryProperty, path);
        }
        finally
        {
            _syncingDirectory = false;
        }

        UpdateNavigationState();
        LoadTask = LoadAsync(path, selectName);
        return true;
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        try
        {
            path = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
        var root = Path.GetPathRoot(path);
        return root != null && string.Equals(root, path, StringComparison.OrdinalIgnoreCase) ? root : path.TrimEnd(Path.DirectorySeparatorChar);
    }

    private void UpdateNavigationState()
    {
        CanGoBack = _backStack.Count > 0;
        CanGoForward = _forwardStack.Count > 0;
        CanGoUp = !string.IsNullOrEmpty(_current);

        var segments = new List<PathSegment> { new("This PC", string.Empty) };
        if (!string.IsNullOrEmpty(_current))
        {
            var root = Path.GetPathRoot(_current) ?? string.Empty;
            segments.Add(new PathSegment(root.TrimEnd(Path.DirectorySeparatorChar), root));
            var accumulated = root;
            foreach (var part in _current[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                accumulated = Path.Combine(accumulated, part);
                segments.Add(new PathSegment(part, accumulated));
            }
        }
        PathSegments = segments;

        // Long paths: keep the current folder (the last crumb) in view.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => _crumbScroller?.ScrollToRightEnd());

        // Keep the places list in sync without re-navigating.
        if (_places != null)
        {
            _syncingPlaces = true;
            try
            {
                _places.SelectedItem = Places.FirstOrDefault(p => string.Equals(p.Path, _current, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _syncingPlaces = false;
            }
        }
    }

    private static void OnCurrentDirectoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var browser = (FileBrowser)d;
        if (!browser._syncingDirectory && browser._list != null) browser.NavigateCore((string?)e.NewValue, recordHistory: true);
    }

    #endregion

    #region Loading

    private const int MaxSearchResults = 2000;

    private sealed record Listing(List<FileEntry> Entries, string? Error, bool Truncated);

    private async Task LoadAsync(string path, string? selectName)
    {
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();
        var token = cts.Token;

        IsLoading = true;
        var filter = Filter;
        var showHidden = ShowHidden;
        var foldersOnly = FoldersOnly;
        var search = SearchText.Trim();
        var column = _sortColumn;
        var descending = _sortDescending;
        var dispatcher = Dispatcher;

        Listing listing;
        try
        {
            listing = await Task.Run(() => Enumerate(path, filter, showHidden, foldersOnly, search, column, descending, token, dispatcher), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // The continuation may run on a pool thread when the caller was not inside a dispatcher callback.
        await Dispatcher.InvokeAsync(() => ApplyListing(token, listing, selectName, foldersOnly, search));
    }

    private void ApplyListing(CancellationToken token, Listing listing, string? selectName, bool foldersOnly, string search)
    {
        if (token.IsCancellationRequested) return;

        var count = listing.Entries.Count;
        Entries = listing.Entries;
        if (listing.Error != null) StatusText = listing.Error;
        else if (count > 0) StatusText = string.Empty;
        else if (search.Length > 0) StatusText = $"No results for “{search}”.";
        else StatusText = foldersOnly ? "There are no folders here." : "This folder is empty.";

        SummaryText = search.Length == 0
            ? $"{count} items"
            : listing.Truncated ? $"First {count} results for “{search}”" : $"{count} result{(count == 1 ? "" : "s")} for “{search}”";
        IsLoading = false;

        if (selectName != null && _list != null)
        {
            var match = listing.Entries.FirstOrDefault(entry => string.Equals(entry.Name, selectName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                _list.SelectedItem = match;
                _list.ScrollIntoView(match);
            }
        }
        DirectoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Listing Enumerate(
        string path, FileFilter? filter, bool showHidden, bool foldersOnly, string search, string sortColumn, bool descending,
        CancellationToken token, System.Windows.Threading.Dispatcher dispatcher)
    {
        var entries = new List<FileEntry>();
        var truncated = false;

        FileEntry? Make(FileSystemInfo info, string location)
        {
            if (info is DirectoryInfo)
            {
                return new FileEntry(info.Name, info.FullName, true, SafeTime(info), null, "File folder", "\uE8B7", location, dispatcher, token);
            }
            if (!foldersOnly && info is FileInfo file && (filter == null || filter.Matches(file.Name)))
            {
                var extension = file.Extension;
                var type = extension.Length > 1 ? extension[1..].ToUpperInvariant() + " file" : "File";
                return new FileEntry(file.Name, file.FullName, false, SafeTime(info), SafeLength(file), type, FileEntry.GlyphFor(extension), location, dispatcher, token);
            }
            return null;
        }

        try
        {
            if (path.Length == 0)
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    token.ThrowIfCancellationRequested();
                    string label = string.Empty;
                    long? size = null;
                    try
                    {
                        if (drive.IsReady)
                        {
                            label = drive.VolumeLabel;
                            size = drive.TotalSize;
                        }
                    }
                    catch (IOException)
                    {
                        // Not ready (e.g. empty card reader): list it without details.
                    }
                    var name = string.IsNullOrEmpty(label) ? $"Local Disk ({drive.Name.TrimEnd(Path.DirectorySeparatorChar)})" : $"{label} ({drive.Name.TrimEnd(Path.DirectorySeparatorChar)})";
                    if (search.Length > 0 && !name.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                    entries.Add(new FileEntry(name, drive.Name, true, null, size, drive.DriveType.ToString(), "\uEDA2"));
                }
            }
            else if (search.Length == 0)
            {
                var directory = new DirectoryInfo(path);
                foreach (var info in directory.EnumerateFileSystemInfos())
                {
                    token.ThrowIfCancellationRequested();
                    if (!showHidden && (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                    if (Make(info, string.Empty) is { } entry) entries.Add(entry);
                }
            }
            else
            {
                // Search the folder and everything below it; inaccessible folders are skipped.
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false,
                    AttributesToSkip = showHidden ? 0 : FileAttributes.Hidden | FileAttributes.System,
                };
                foreach (var info in new DirectoryInfo(path).EnumerateFileSystemInfos("*", options))
                {
                    token.ThrowIfCancellationRequested();
                    if (!info.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;

                    var parent = (info is FileInfo file ? file.Directory : (info as DirectoryInfo)?.Parent)?.FullName ?? path;
                    var location = parent.Length > path.Length ? Path.GetRelativePath(path, parent) : string.Empty;
                    if (Make(info, location) is not { } entry) continue;

                    if (entries.Count >= MaxSearchResults)
                    {
                        truncated = true;
                        break;
                    }
                    entries.Add(entry);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new Listing([], "You don't have permission to open this folder.", false);
        }
        catch (Exception ex) when (ex is IOException or DirectoryNotFoundException)
        {
            return new Listing([], ex.Message, false);
        }

        return new Listing(Sort(entries, sortColumn, descending), null, truncated);
    }

    private static DateTime? SafeTime(FileSystemInfo info)
    {
        try
        {
            return info.LastWriteTime;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static long? SafeLength(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static List<FileEntry> Sort(List<FileEntry> entries, string column, bool descending)
    {
        IOrderedEnumerable<FileEntry> ordered = entries.OrderByDescending(e => e.IsDirectory);
        Func<FileEntry, object?> key = column switch
        {
            "Date modified" => e => e.Modified,
            "Type" => e => e.TypeName,
            "Size" => e => e.Size ?? -1,
            _ => e => e.Name,
        };
        var comparer = column is "Name" or "Type" ? (IComparer<object?>)new TextComparer() : Comparer<object?>.Default;
        ordered = descending ? ordered.ThenByDescending(key, comparer) : ordered.ThenBy(key, comparer);
        if (column != "Name") ordered = ordered.ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase);
        return ordered.ToList();
    }

    private sealed class TextComparer : IComparer<object?>
    {
        public int Compare(object? x, object? y) => StringComparer.CurrentCultureIgnoreCase.Compare(x as string, y as string);
    }

    #endregion

    #region Events from the template

    private void OnBackClick(object sender, RoutedEventArgs e) => GoBack();

    private void OnForwardClick(object sender, RoutedEventArgs e) => GoForward();

    private void OnUpClick(object sender, RoutedEventArgs e) => GoUp();

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Refresh();

    private void OnCrumbClicked(object? sender, BreadcrumbBarItemClickedEventArgs e)
    {
        if (e.Item is PathSegment segment) Navigate(segment.Path);
    }

    private void OnPlaceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingPlaces || _places?.SelectedItem is not FilePlace place) return;
        Navigate(place.Path);
    }

    private void OnPathHostMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || IsEditingPath || _pathBox == null) return;
        BeginPathEdit();
        e.Handled = true;
    }

    private void BeginPathEdit()
    {
        if (_pathBox == null) return;
        IsEditingPath = true;
        _pathBox.Text = _current ?? string.Empty;
        _pathBox.UpdateLayout();
        _pathBox.Focus();
        _pathBox.SelectAll();
    }

    private void EndPathEdit()
    {
        IsEditingPath = false;
        _list?.Focus();
    }

    private void OnPathBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Navigate(_pathBox?.Text)) EndPathEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            EndPathEdit();
            e.Handled = true;
        }
    }

    private void OnPathBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (IsEditingPath) IsEditingPath = false;
    }

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e) => SelectedEntriesChanged?.Invoke(this, EventArgs.Empty);

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not ListViewItem) source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        if (source is ListViewItem { Content: FileEntry entry })
        {
            Activate(entry);
            e.Handled = true;
        }
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when _list?.SelectedItem is FileEntry entry:
                Activate(entry);
                e.Handled = true;
                break;
            case Key.Back:
                GoUp();
                e.Handled = true;
                break;
            case Key.Left when Keyboard.Modifiers == ModifierKeys.Alt:
                GoBack();
                e.Handled = true;
                break;
            case Key.Right when Keyboard.Modifiers == ModifierKeys.Alt:
                GoForward();
                e.Handled = true;
                break;
            case Key.Up when Keyboard.Modifiers == ModifierKeys.Alt:
                GoUp();
                e.Handled = true;
                break;
            case Key.F5:
                Refresh();
                e.Handled = true;
                break;
        }
    }

    private void Activate(FileEntry entry)
    {
        if (entry.IsDirectory) Navigate(entry.FullPath);
        else EntryActivated?.Invoke(this, entry);
    }

    private void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Role: GridViewColumnHeaderRole.Normal, Column.Header: string header }) return;

        _sortDescending = header == _sortColumn && !_sortDescending;
        _sortColumn = header;
        Entries = Sort(Entries.ToList(), _sortColumn, _sortDescending);
    }

    private async void OnNewFolderClick(object sender, RoutedEventArgs e) => await CreateFolderAsync();

    /// <summary>Asks for a name and creates a folder in the current directory.</summary>
    public async Task CreateFolderAsync()
    {
        if (string.IsNullOrEmpty(_current)) return;

        var name = new TextBox { Text = "New folder" };
        ControlHelper.SetHeader(name, "Folder name");
        var error = new TextBlock { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        error.SetResourceReference(TextBlock.ForegroundProperty, "Tessel.DangerBrush");

        var dialog = new ContentDialog
        {
            Title = "New folder",
            Content = new StackPanel { Children = { name, error } },
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
        };
        string? created = null;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var text = name.Text.Trim();
            string? problem = null;
            if (text.Length == 0 || text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) problem = "Enter a valid folder name.";
            else if (Directory.Exists(Path.Combine(_current, text)) || File.Exists(Path.Combine(_current, text))) problem = "An item with this name already exists.";
            else
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(_current, text));
                    created = text;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    problem = ex.Message;
                }
            }
            if (problem != null)
            {
                error.Text = problem;
                error.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        await dialog.ShowAsync(Window.GetWindow(this));
        if (created != null) await LoadAsync(_current, created);
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var browser = (FileBrowser)d;
        if (browser._syncingSearch) return;
        browser._searchTimer.Stop();
        browser._searchTimer.Start();
    }

    private void ClearSearch()
    {
        _searchTimer.Stop();
        if (SearchText.Length == 0) return;
        _syncingSearch = true;
        try
        {
            SetCurrentValue(SearchTextProperty, string.Empty);
        }
        finally
        {
            _syncingSearch = false;
        }
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _searchTimer.Stop();
                Refresh();
                e.Handled = true;
                break;
            case Key.Escape when SearchText.Length > 0:
                ClearSearch();
                Refresh();
                e.Handled = true;
                break;
            case Key.Down:
                _list?.Focus();
                e.Handled = true;
                break;
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && _searchBox != null)
        {
            _searchBox.Focus();
            _searchBox.SelectAll();
            e.Handled = true;
        }
    }

    private static void OnViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FileBrowser)d).SyncViewSwitch();

    private void SyncViewSwitch()
    {
        if (_viewSwitch == null) return;
        _syncingView = true;
        try
        {
            _viewSwitch.SelectedIndex = (int)ViewMode;
        }
        finally
        {
            _syncingView = false;
        }
    }

    private void OnViewSwitchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingView || _viewSwitch is not { SelectedIndex: >= 0 } switcher) return;
        SetCurrentValue(ViewModeProperty, (FileBrowserView)switcher.SelectedIndex);
    }

    private void UpdateSelectionMode()
    {
        if (_list != null) _list.SelectionMode = Multiselect ? SelectionMode.Extended : SelectionMode.Single;
    }

    #endregion
}
