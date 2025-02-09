using System.Runtime.InteropServices;

namespace Notio.Management.System;

/// <summary>
/// Provides methods to retrieve memory usage information.
/// </summary>
public static class InfoMemory
{
    /// <summary>
    /// Retrieves memory usage details of the system.
    /// </summary>
    /// <returns>A string containing memory usage information.</returns>
    public static string GetUsage()
    {
        string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value"
            : "free -m";

        return SystemInfo.RunCommand(command).ParseMemory();
    }
}