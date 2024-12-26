using System.Runtime.InteropServices;

namespace Notio.Infrastructure.Management;

public static class InfoOS
{
    /// <summary>
    /// Lấy tên của hệ điều hành đang chạy.
    /// </summary>
    /// <returns>Chuỗi tên hệ điều hành hoặc "Unsupported OS" nếu không phải Windows hoặc Linux.</returns>
    public static string GetOperatingSystem() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
        ? "Windows" 
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) 
        ? "Linux" 
        : "Unsupported OS";

    /// <summary>
    /// Lấy thông tin chi tiết về hệ điều hành.
    /// </summary>
    /// <returns>Chuỗi mô tả chi tiết hệ điều hành hoặc thông báo lỗi.</returns>
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