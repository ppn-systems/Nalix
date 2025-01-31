using Notio.Shared.Management.Commands;
using System.Runtime.InteropServices;

namespace Notio.Shared.Management.System;

public static class InfoMemory
{
    /// <summary>
    /// Lấy thông tin về việc sử dụng bộ nhớ.
    /// </summary>
    public static string Usage()
    {
        string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value"
            : "free -m";

        return SystemInfo.RunCommand(command).ParseMemory();
    }
}