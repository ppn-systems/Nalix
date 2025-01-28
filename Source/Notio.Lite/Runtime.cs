using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Notio.Lite;

/// <summary>
/// Provides utility methods and properties for runtime information specific to the Notio application.
/// </summary>
public static class Runtime
{
    private static readonly Lazy<Assembly> EntryAssemblyLazy = new(Assembly.GetEntryAssembly!);
    private static readonly Lazy<string> CompanyNameLazy = new(() => (EntryAssembly.GetCustomAttribute<AssemblyCompanyAttribute>() as AssemblyCompanyAttribute)?.Company ?? string.Empty);
    private static readonly Lazy<string> ProductNameLazy = new(() => (EntryAssembly.GetCustomAttribute<AssemblyProductAttribute>() as AssemblyProductAttribute)?.Product ?? string.Empty);
    private static readonly Lazy<string> ProductTrademarkLazy = new(() => (EntryAssembly.GetCustomAttribute<AssemblyTrademarkAttribute>() as AssemblyTrademarkAttribute)?.Trademark ?? string.Empty);

    private static readonly string ApplicationMutexName = "Global\\{{" + EntryAssembly.FullName + "}}";
    private static readonly Lock SyncLock = new();
    private static OperatingSystem? _oS;

    /// <summary>
    /// Gets the operating system type on which the application is running.
    /// </summary>
    public static OperatingSystem OS
    {
        get
        {
            if (!_oS.HasValue)
            {
                string? environmentVariable = Environment.GetEnvironmentVariable("windir");
                if (!string.IsNullOrEmpty(environmentVariable)
                    && environmentVariable.Contains('\\')
                    && Directory.Exists(environmentVariable))
                {
                    _oS = OperatingSystem.Windows;
                }
                else
                {
                    _oS = (File.Exists("/proc/sys/kernel/ostype") ? OperatingSystem.Unix : OperatingSystem.Osx);
                }
            }

            return _oS.GetValueOrDefault();
        }
    }

    /// <summary>
    /// Determines whether this is the only running instance of the application.
    /// </summary>
    public static bool IsTheOnlyInstance
    {
        get
        {
            lock (SyncLock)
            {
                try
                {
                    using (Mutex.OpenExisting(ApplicationMutexName))
                    {
                    }
                }
                catch
                {
                    try
                    {
                        using Mutex arg = new(initiallyOwned: true, ApplicationMutexName);
                        return true;
                    }
                    catch
                    {
                    }
                }

                return false;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the application is running under the Mono runtime.
    /// </summary>
    public static bool IsUsingMonoRuntime => Type.GetType("Mono.Runtime") != null;

    /// <summary>
    /// Gets the entry assembly of the application.
    /// </summary>
    public static Assembly EntryAssembly => EntryAssemblyLazy.Value;

    /// <summary>
    /// Gets the name of the entry assembly.
    /// </summary>
    public static AssemblyName EntryAssemblyName => EntryAssemblyLazy.Value.GetName();

    /// <summary>
    /// Gets the version of the entry assembly.
    /// </summary>
    public static Version? EntryAssemblyVersion => EntryAssemblyName.Version;

    /// <summary>
    /// Gets the directory path of the entry assembly.
    /// </summary>
    public static string? EntryAssemblyDirectory => Path.GetDirectoryName(EntryAssembly?.Location);

    /// <summary>
    /// Gets the company name from the assembly's metadata.
    /// </summary>
    public static string CompanyName => CompanyNameLazy.Value;

    /// <summary>
    /// Gets the product name from the assembly's metadata.
    /// </summary>
    public static string ProductName => ProductNameLazy.Value;

    /// <summary>
    /// Gets the product trademark from the assembly's metadata.
    /// </summary>
    public static string ProductTrademark => ProductTrademarkLazy.Value;

    /// <summary>
    /// Gets the local storage path for the application, creating it if it does not exist.
    /// </summary>
    public static string LocalStoragePath
    {
        get
        {
            string entryAssemblyName = EntryAssemblyName.Name ??
                throw new InvalidOperationException("EntryAssemblyName.Name is null");

            string entryAssemblyVersion = EntryAssemblyVersion?.ToString() ??
                throw new InvalidOperationException("EntryAssemblyVersion is null");

            string text = Path.Combine(Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), entryAssemblyName), entryAssemblyVersion);

            if (!Directory.Exists(text))
            {
                Directory.CreateDirectory(text);
            }

            return text;
        }
    }

    /// <summary>
    /// Gets the full path to a file on the desktop with the specified filename.
    /// </summary>
    /// <param name="filename">The name of the file.</param>
    /// <returns>The full path to the file on the desktop.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the filename is null or whitespace.</exception>
    public static string GetDesktopFilePath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            throw new ArgumentNullException(nameof(filename));
        }

        return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), filename));
    }
}