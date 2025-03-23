using System;
using System.IO;

namespace Notio.Runtime;

/// <summary>
/// Provides utility methods for managing local storage paths.
/// </summary>
public static class LocalStorage
{
    private static readonly Lazy<string> LocalStoragePathLazy = new(() =>
    {
        string assemblyName = ApplicationInfo.EntryAssemblyName.Name
            ?? throw new InvalidOperationException("Entry assembly name is null.");

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string basePath = Path.Combine(localAppData, assemblyName);
        string version = ApplicationInfo.EntryAssemblyVersion?.ToString()
            ?? throw new InvalidOperationException("Entry assembly version is null.");

        string fullPath = Path.Combine(basePath, version);

        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);

        return fullPath;
    });

    /// <summary>
    /// Gets the local storage path with a version.
    /// </summary>
    public static string LocalStoragePath => LocalStoragePathLazy.Value;

    /// <summary>
    /// Builds a full path pointing to the current user's desktop with the given filename.
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// <returns>The fully qualified path.</returns>
    /// <exception cref="ArgumentNullException">Thrown if filename is null or whitespace.</exception>
    public static string GetDesktopFilePath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentNullException(nameof(filename));

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string fullPath = Path.Combine(desktopPath, filename);

        return Path.GetFullPath(fullPath);
    }
}
