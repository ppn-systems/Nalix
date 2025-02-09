using System.Runtime.InteropServices;

namespace Notio.Management.System;

/// <summary>
/// Provides methods to retrieve information about the operating system.
/// </summary>
public static class InfoOS
{
    /// <summary>
    /// Gets the name of the current operating system.
    /// </summary>
    /// <returns>A string representing the name of the operating system.</returns>
    public static string GetOperatingSystem() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" :
        "Unsupported OS";

    /// <summary>
    /// Retrieves detailed information about the operating system.
    /// </summary>
    /// <returns>A string containing the OS details, or "Unsupported OS" if not recognized.</returns>
    public static string GetDetails()
    {
        string? command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "wmic os get caption"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? "lsb_release -a"
            : null;

        return command != null ? SystemInfo.RunCommand(command).ParseDefault() : "Unsupported OS";
    }
}