using System;
using System.Reflection;
using Tessel.NET.Controls;

namespace Tessel.Gallery;

/// <summary>Version information shown in the gallery, read from the Tessel.NET assembly.</summary>
public static class AppInfo
{
    private static readonly Version LibraryVersion = typeof(TesselWindow).Assembly.GetName().Version ?? new Version(1, 0);

    /// <summary>e.g. "1.1"</summary>
    public static string ShortVersion => $"{LibraryVersion.Major}.{LibraryVersion.Minor}";

    /// <summary>e.g. "v1.1" (title-bar badge).</summary>
    public static string Badge => "v" + ShortVersion;

    /// <summary>e.g. "Tessel UI 1.1"</summary>
    public static string ProductName => "Tessel UI " + ShortVersion;
}
