using System.Runtime.InteropServices;

namespace Notio.Infrastructure.Management;

public static class InfoOS
{
    /// <summary>
    /// Lấy tên của hệ điều hành đang chạy.
    /// </summary>
    public static string GetOperatingSystem() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "Windows"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        ? "Linux"
        : "Unsupported OS";

    /// <summary>
    /// Lấy thông tin chi tiết về hệ điều hành.
    /// </summary>
    public static string Details()
    {
        string? command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "wmic os get caption"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? "lsb_release -a"
            : null;

        return command != null ? SystemInfo.RunCommand(command).ParseDefault() : "Unsupported OS";
    }
}