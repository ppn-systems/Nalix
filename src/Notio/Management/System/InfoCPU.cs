using System.Runtime.InteropServices;

namespace Notio.Management.System;

/// <summary>
/// Provides methods to retrieve CPU-related information.
/// </summary>
public static class InfoCPU
{
    /// <summary>
    /// Retrieves the CPU name on Windows and Linux.
    /// </summary>
    /// <returns>The name of the CPU.</returns>
    public static string GetName()
    {
        string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "wmic cpu get name"
            : "lscpu | grep 'Model name' | awk -F: '{print $2}'";

        return SystemInfo.RunCommand(command).ParseDefault();
    }

    /// <summary>
    /// Retrieves the current CPU usage percentage on Windows and Linux.
    /// </summary>
    /// <returns>A string representing CPU load percentage.</returns>
    public static string GetUsage()
    {
        string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "wmic cpu get loadpercentage"
            : "top -bn1 | grep 'Cpu(s)' | awk '{print $2 + $4}'";

        return SystemInfo.RunCommand(command).ParseCPU();
    }
}