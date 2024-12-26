using System.Runtime.InteropServices;

namespace Notio.Infrastructure.Management;

public static class InfoMemory
{
    /// <summary>
    /// Lấy thông tin về việc sử dụng bộ nhớ.
    /// </summary>
    /// <returns>Chuỗi mô tả trạng thái bộ nhớ hoặc thông báo lỗi.</returns>
    public static string Usage()
    {
        string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value"
            : "free -m";

        return SystemInfo.RunCommand(command).ParseMemory();
    }
}
