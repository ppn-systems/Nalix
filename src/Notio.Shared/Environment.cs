using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Notio.Shared;

/// <summary>
/// Enumeration of Operating Systems.
/// </summary>
public enum OSType
{
    /// <summary>
    /// Unknown OS
    /// </summary>
    Unknown,

    /// <summary>
    /// Windows
    /// </summary>
    Windows,

    /// <summary>
    /// UNIX/Linux
    /// </summary>
    Unix,

    /// <summary>
    /// macOS (OSX)
    /// </summary>
    Osx,
}

/// <summary>
/// Defines Endianness, big or little.
/// </summary>
public enum Endianness
{
    /// <summary>
    /// In big endian, you store the most significant byte in the smallest address.
    /// </summary>
    Big,

    /// <summary>
    /// In little endian, you store the least significant byte in the smallest address.
    /// </summary>
    Little,
}

/// <summary>
/// Provides utility methods to retrieve information about the current application.
/// </summary>
public static class Environment
{
    // Lazy-load the entry assembly.
    private static readonly Lazy<Assembly> EntryAssemblyLazy = new(() =>
        Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Entry assembly is null."));

    // Lazy-loaded company, product, and trademark names.
    private static readonly Lazy<string> CompanyNameLazy = new(() =>
    {
        var attribute = EntryAssembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        return attribute?.Company ?? string.Empty;
    });

    private static readonly Lazy<string> ProductNameLazy = new(() =>
    {
        var attribute = EntryAssembly.GetCustomAttribute<AssemblyProductAttribute>();
        return attribute?.Product ?? string.Empty;
    });

    private static readonly Lazy<string> ProductTrademarkLazy = new(() =>
    {
        var attribute = EntryAssembly.GetCustomAttribute<AssemblyTrademarkAttribute>();
        return attribute?.Trademark ?? string.Empty;
    });

    // Application mutex name based on the entry assembly’s full name.
    private static readonly string ApplicationMutexName = "Global\\{{" + EntryAssembly.FullName + "}}";

    // Use a simple static object for synchronization.
    private static readonly Lock SyncLock = new();

    // Lazy-load the operating system type.
    private static readonly Lazy<OSType> OsLazy = new(() =>
    {
        var windir = System.Environment.GetEnvironmentVariable("windir");
        if (!string.IsNullOrEmpty(windir) && windir.Contains('\\') && Directory.Exists(windir))
        {
            return OSType.Windows;
        }
        // Check for Unix-like system by the existence of /proc/sys/kernel/ostype
        return File.Exists("/proc/sys/kernel/ostype") ? OSType.Unix : OSType.Osx;
    });

    // Lazy-load the local storage path.
    private static readonly Lazy<string> LocalStoragePathLazy = new(() =>
    {
        var assemblyName = EntryAssemblyName.Name
            ?? throw new InvalidOperationException("Entry assembly name is null.");
        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        var basePath = Path.Combine(localAppData, assemblyName);
        var version = EntryAssemblyVersion?.ToString()
            ?? throw new InvalidOperationException("Entry assembly version is null.");
        var fullPath = Path.Combine(basePath, version);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }
        return fullPath;
    });

    #region Properties

    /// <summary>
    /// Gets the current operating system.
    /// </summary>
    public static OSType OS => OsLazy.Value;

    /// <summary>
    /// Checks if this application (including version number) is the only instance currently running.
    /// </summary>
    public static bool IsTheOnlyInstance
    {
        get
        {
            lock (SyncLock)
            {
                try
                {
                    // Try to open an existing global mutex.
                    using var existingMutex = Mutex.OpenExisting(ApplicationMutexName);
                }
                catch
                {
                    try
                    {
                        // If no mutex exists, create one. This instance is the only instance.
                        using var appMutex = new Mutex(true, ApplicationMutexName);
                        return true;
                    }
                    catch
                    {
                        // In case mutex creation fails.
                    }
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether this application instance is using the MONO runtime.
    /// </summary>
    public static bool IsUsingMonoRuntime => Type.GetType("Mono.Environment") != null;

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
    public static Version? EntryAssemblyVersion => EntryAssemblyName.Version;

    /// <summary>
    /// Gets the full path to the folder containing the assembly that started the application.
    /// </summary>
    public static string EntryAssemblyDirectory
    {
        get
        {
            var location = EntryAssembly.Location;
            if (string.IsNullOrEmpty(location))
                throw new InvalidOperationException("Entry assembly location is null or empty.");
            var uri = new UriBuilder(location);
            var path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Failed to get directory name from path.");
        }
    }

    /// <summary>
    /// Gets the company name.
    /// </summary>
    public static string CompanyName => CompanyNameLazy.Value;

    /// <summary>
    /// Gets the product name.
    /// </summary>
    public static string ProductName => ProductNameLazy.Value;

    /// <summary>
    /// Gets the product trademark.
    /// </summary>
    public static string ProductTrademark => ProductTrademarkLazy.Value;

    /// <summary>
    /// Gets the local storage path with a version.
    /// </summary>
    public static string LocalStoragePath => LocalStoragePathLazy.Value;

    #endregion Properties

    #region Methods

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
        var desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
        var fullPath = Path.Combine(desktopPath, filename);
        return Path.GetFullPath(fullPath);
    }

    #endregion Methods
}
