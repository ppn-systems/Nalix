using Notio.Common.Enums;
using System;
using System.IO;

namespace Notio.Runtime;

/// <summary>
/// Provides utility methods related to the operating system.
/// </summary>
public static class OperatingSystem
{
    private static readonly Lazy<OSType> OsLazy = new(() =>
    {
        var windir = Environment.GetEnvironmentVariable("windir");
        if (!string.IsNullOrEmpty(windir) && windir.Contains('\\') && Directory.Exists(windir))
        {
            return OSType.Windows;
        }
        // Check for Unix-like system by the existence of /proc/sys/kernel/ostype
        return File.Exists("/proc/sys/kernel/ostype") ? OSType.Unix : OSType.Osx;
    });

    /// <summary>
    /// Gets the current operating system.
    /// </summary>
    public static OSType OS => OsLazy.Value;
}
