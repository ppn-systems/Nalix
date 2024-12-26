using System.Runtime.InteropServices;

namespace Notio.Infrastructure.Management;

public static class InfoCPU
{
    /// <summary>
    /// Lấy tên của CPU trên Windows và Linux.
    /// </summary>
    /// <returns>Chuỗi tên CPU hoặc thông báo lỗi.</returns>
    public static string Name()
    {
        string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "wmic cpu get name"
            : "lscpu | grep 'Model name' | awk -F: '{print $2}'";

        return SystemInfo.RunCommand(command).ParseDefault();
    }

    /// <summary>
    /// Lấy thông tin về phần trăm tải CPU trên Windows và Linux.
    /// </summary>
    /// <returns>Chuỗi phần trăm tải CPU hoặc thông báo lỗi.</returns>
    public static string Usage()
    {
        string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "wmic cpu get loadpercentage"
            : "top -bn1 | grep 'Cpu(s)' | awk '{print $2 + $4}'";

        return SystemInfo.RunCommand(command).ParseCPU();
    }
}
