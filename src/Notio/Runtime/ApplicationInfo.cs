using System;
using System.IO;
using System.Reflection;

namespace Notio.Runtime;

/// <summary>
/// Provides utility methods to retrieve information about the current application.
/// </summary>
public static class ApplicationInfo
{
    // Lazy-load the entry assembly.
    private static readonly Lazy<Assembly> EntryAssemblyLazy = new(() =>
        Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Entry assembly is null."));

    /// <summary>
    /// Gets the assembly that started the application.
    /// </summary>
    public static Assembly EntryAssembly => EntryAssemblyLazy.Value;

    /// <summary>
    /// Gets the name of the entry assembly.
    /// </summary>
    public static AssemblyName EntryAssemblyName => EntryAssembly.GetName();

    /// <summary>
    /// Gets the entry assembly version.
    /// </summary>
    public static Version EntryAssemblyVersion => EntryAssemblyName.Version;

    /// <summary>
    /// Gets the full path to the folder containing the assembly that started the application.
    /// </summary>
    public static string EntryAssemblyDirectory
    {
        get
        {
            string location = EntryAssembly.Location;
            if (string.IsNullOrEmpty(location))
                throw new InvalidOperationException("Entry assembly location is null or empty.");

            UriBuilder uri = new(location);
            string path = Uri.UnescapeDataString(uri.Path);

            return Path.GetDirectoryName(path) ??
                throw new InvalidOperationException("Failed to get directory name from path.");
        }
    }
}
