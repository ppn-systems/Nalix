using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Notio.Lite;

public static class NotioRuntime
{
    private static readonly Lazy<Assembly> EntryAssemblyLazy = new(Assembly.GetEntryAssembly!);

    private static readonly Lazy<string> CompanyNameLazy = new(() => (EntryAssembly.GetCustomAttribute<AssemblyCompanyAttribute>() as AssemblyCompanyAttribute)?.Company ?? string.Empty);

    private static readonly Lazy<string> ProductNameLazy = new(() => (EntryAssembly.GetCustomAttribute<AssemblyProductAttribute>() as AssemblyProductAttribute)?.Product ?? string.Empty);

    private static readonly Lazy<string> ProductTrademarkLazy = new(() => (EntryAssembly.GetCustomAttribute<AssemblyTrademarkAttribute>() as AssemblyTrademarkAttribute)?.Trademark ?? string.Empty);

    private static readonly string ApplicationMutexName = "Global\\{{" + EntryAssembly.FullName + "}}";

    private static readonly Lock SyncLock = new();

    private static OperatingSystem? _oS;

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

    public static bool IsUsingMonoRuntime => Type.GetType("Mono.Runtime") != null;

    public static Assembly EntryAssembly => EntryAssemblyLazy.Value;

    public static AssemblyName EntryAssemblyName => EntryAssemblyLazy.Value.GetName();

    public static Version? EntryAssemblyVersion => EntryAssemblyName.Version;

    public static string? EntryAssemblyDirectory => Path.GetDirectoryName(EntryAssembly?.Location);

    public static string CompanyName => CompanyNameLazy.Value;

    public static string ProductName => ProductNameLazy.Value;

    public static string ProductTrademark => ProductTrademarkLazy.Value;

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

    public static string GetDesktopFilePath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            throw new ArgumentNullException(nameof(filename));
        }

        return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), filename));
    }
}