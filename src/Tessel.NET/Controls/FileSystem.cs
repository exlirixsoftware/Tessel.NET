using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Tessel.NET.Controls;

/// <summary>
/// A named set of file name patterns, e.g. <c>new FileFilter("Images", "*.png;*.jpg")</c>.
/// Use <see cref="Parse"/> for the classic <c>"Images|*.png;*.jpg|All files|*.*"</c> format.
/// </summary>
public sealed class FileFilter
{
    public FileFilter(string name, string patterns)
    {
        Name = name;
        Patterns = patterns
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .DefaultIfEmpty("*.*")
            .ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<string> Patterns { get; }

    /// <summary>The name and patterns, e.g. <c>Images (*.png, *.jpg)</c>.</summary>
    public string DisplayName => $"{Name} ({string.Join(", ", Patterns)})";

    /// <summary>All files (<c>*.*</c>).</summary>
    public static FileFilter AllFiles => new("All files", "*.*");

    /// <summary>The extension (with dot) a file saved with this filter gets by default, or null for wildcard patterns.</summary>
    public string? DefaultExtension
    {
        get
        {
            foreach (var pattern in Patterns)
            {
                var extension = Path.GetExtension(pattern);
                if (extension.Length > 1 && !extension.Contains('*') && !extension.Contains('?')) return extension;
            }
            return null;
        }
    }

    public bool Matches(string fileName)
    {
        foreach (var pattern in Patterns)
        {
            if (pattern is "*" or "*.*") return true;
            if (FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true)) return true;
        }
        return false;
    }

    /// <summary>Parses <c>"Name|patterns|Name|patterns"</c>. Returns <see cref="AllFiles"/> for an empty or malformed string.</summary>
    public static IReadOnlyList<FileFilter> Parse(string? filter)
    {
        var result = new List<FileFilter>();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var parts = filter.Split('|');
            for (var i = 0; i + 1 < parts.Length; i += 2)
            {
                result.Add(new FileFilter(parts[i].Trim(), parts[i + 1]));
            }
        }
        if (result.Count == 0) result.Add(AllFiles);
        return result;
    }

    public override string ToString() => DisplayName;
}

/// <summary>How a <see cref="FileBrowser"/> lists its entries.</summary>
public enum FileBrowserView
{
    /// <summary>A table with name, date modified, type and size.</summary>
    Details,

    /// <summary>Tiles with an image preview (or a large icon) and the name.</summary>
    Thumbnails,
}

/// <summary>A file or folder shown in a <see cref="FileBrowser"/>.</summary>
public sealed class FileEntry : INotifyPropertyChanged
{
    private readonly Dispatcher? _dispatcher;
    private readonly CancellationToken _token;
    private ImageSource? _thumbnail;
    private bool _thumbnailRequested;

    internal FileEntry(
        string name,
        string fullPath,
        bool isDirectory,
        DateTime? modified,
        long? size,
        string typeName,
        string glyph,
        string location = "",
        Dispatcher? dispatcher = null,
        CancellationToken token = default)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Modified = modified;
        Size = size;
        TypeName = typeName;
        Glyph = glyph;
        Location = location;
        IsImage = !isDirectory && ThumbnailLoader.IsImage(name);
        _dispatcher = dispatcher;
        _token = token;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public string FullPath { get; }

    public bool IsDirectory { get; }

    public DateTime? Modified { get; }

    public long? Size { get; }

    public string TypeName { get; }

    /// <summary>Icon-font glyph for the entry.</summary>
    public string Glyph { get; }

    /// <summary>For search results: the folder containing the entry, relative to the folder that was searched.</summary>
    public string Location { get; }

    /// <summary>True for files with a picture extension that can get a <see cref="Thumbnail"/>.</summary>
    public bool IsImage { get; }

    public string ModifiedText => Modified?.ToString("g") ?? string.Empty;

    public string SizeText => Size is { } size ? FormatSize(size) : string.Empty;

    /// <summary>
    /// A small preview of an image file. It is decoded in the background the first time it is read
    /// (the property raises <see cref="PropertyChanged"/> when it is ready); null for everything else.
    /// </summary>
    public ImageSource? Thumbnail
    {
        get
        {
            if (!_thumbnailRequested && IsImage)
            {
                _thumbnailRequested = true;
                RequestThumbnail();
            }
            return _thumbnail;
        }
    }

    private void RequestThumbnail()
    {
        var path = FullPath;
        var token = _token;
        var dispatcher = _dispatcher ?? System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        _ = Task.Run(async () =>
        {
            ImageSource? image;
            try
            {
                image = await ThumbnailLoader.LoadAsync(path, token);
            }
            catch (OperationCanceledException)
            {
                // The folder was left before the preview was decoded; a later read may ask again.
                _thumbnailRequested = false;
                return;
            }
            if (image == null) return;
            await dispatcher.InvokeAsync(() =>
            {
                _thumbnail = image;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            });
        });
    }

    internal static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }

    internal static string GlyphFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" or ".tif" or ".tiff" => "",
        ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a" or ".aac" => "",
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" => "",
        ".txt" or ".md" or ".doc" or ".docx" or ".pdf" or ".rtf" or ".odt" => "",
        ".cs" or ".xaml" or ".json" or ".xml" or ".html" or ".css" or ".js" or ".ts" or ".py" or ".cpp" or ".h" or ".java" or ".sh" or ".bat" or ".ps1" => "",
        _ => "",
    };
}

/// <summary>Decodes small previews of image files, with a cache and a limit on parallel decoding.</summary>
internal static class ThumbnailLoader
{
    private const int DecodeWidth = 192;
    private const int CacheLimit = 800;

    private static readonly SemaphoreSlim Gate = new(3);
    private static readonly Dictionary<string, (DateTime Modified, ImageSource Image)> Cache = new(StringComparer.OrdinalIgnoreCase);

    // Formats WPF can decode without extra codecs.
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".ico", ".jfif",
    };

    public static bool IsImage(string fileName) => Extensions.Contains(Path.GetExtension(fileName));

    public static async Task<ImageSource?> LoadAsync(string path, CancellationToken token)
    {
        DateTime modified;
        try
        {
            modified = File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var hit) && hit.Modified == modified) return hit.Image;
        }

        await Gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var image = Decode(path);
            if (image != null)
            {
                lock (Cache)
                {
                    if (Cache.Count >= CacheLimit) Cache.Clear();
                    Cache[path] = (modified, image);
                }
            }
            return image;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static ImageSource? Decode(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.DecodePixelWidth = DecodeWidth;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException or UnauthorizedAccessException
            or ArgumentException or UriFormatException)
        {
            // Corrupt or unsupported picture: the tile keeps its icon.
            return null;
        }
    }
}

/// <summary>One crumb of the path shown above a <see cref="FileBrowser"/>.</summary>
public sealed class PathSegment
{
    internal PathSegment(string name, string path)
    {
        Name = name;
        Path = path;
    }

    public string Name { get; }

    /// <summary>The directory the crumb navigates to (empty for "This PC").</summary>
    public string Path { get; }

    public override string ToString() => Name;
}

/// <summary>A shortcut shown in the side list of a <see cref="FileBrowser"/> (Documents, a drive, ...).</summary>
public sealed class FilePlace
{
    internal FilePlace(string name, string path, string glyph)
    {
        Name = name;
        Path = path;
        Glyph = glyph;
    }

    public string Name { get; }

    public string Path { get; }

    public string Glyph { get; }

    internal static List<FilePlace> CreateDefaults()
    {
        var places = new List<FilePlace>();

        void Add(string name, string? path, string glyph)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) places.Add(new FilePlace(name, path, glyph));
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Add("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "");
        Add("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "");
        Add("Downloads", profile.Length > 0 ? System.IO.Path.Combine(profile, "Downloads") : null, "");
        Add("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "");
        Add("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "");
        Add("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "");

        places.Add(new FilePlace("This PC", string.Empty, ""));
        return places;
    }
}
