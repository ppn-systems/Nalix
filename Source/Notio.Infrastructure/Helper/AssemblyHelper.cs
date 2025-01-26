using System;
using System.Globalization;
using System.Reflection;

namespace Notio.Infrastructure.Helper;

/// <summary>
/// Lớp helper cho các tác vụ liên quan đến Assembly.
/// </summary>
public static class AssemblyHelper
{
    /// <summary>
    /// Trả về phiên bản của assembly đang gọi phương thức này dưới dạng chuỗi.
    /// </summary>
    public static string GetAssemblyVersion()
    {
        Assembly assembly = Assembly.GetCallingAssembly();
        Version? version = assembly.GetName().Version;

        return version?.ToString() ?? "Unknown Version";
    }

    /// <summary>
    /// Trả về thông tin phiên bản của assembly đang gọi phương thức này dưới dạng chuỗi.
    /// Thông tin phiên bản được lấy từ thuộc tính <see cref="AssemblyInformationalVersionAttribute"/>.
    /// </summary>
    public static string GetAssemblyInformationalVersion()
    {
        var assembly = Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        // Kiểm tra xem thuộc tính thông tin phiên bản có được định nghĩa không
        if (attribute?.InformationalVersion == null)
            return string.Empty;

        // Trả về chuỗi thông tin trước ký tự '+', thường là phần build time
        return attribute.InformationalVersion.Split('+')[0];
    }

    /// <summary>
    /// Phân tích thời gian build của assembly dựa trên chuỗi thông tin từ <see cref="AssemblyInformationalVersionAttribute"/>.
    /// </summary>
    /// <param name="prefix">Chuỗi tiền tố để tìm thời gian build (ví dụ: "+build").</param>
    /// <param name="format">Định dạng ngày giờ của thời gian build (mặc định: "yyyyMMddHHmmss").</param>
    /// <returns>Thời gian build dưới dạng <see cref="DateTime"/> hoặc <c>default</c> nếu không thể phân tích.</returns>
    public static DateTime ParseAssemblyBuildTime(string prefix = "+build", string format = "yyyyMMddHHmmss")
    {
        var assembly = Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        // Kiểm tra xem thuộc tính chứa thời gian build có được định nghĩa không
        if (attribute?.InformationalVersion == null)
            return default;

        // Tìm vị trí của chuỗi tiền tố (prefix) trong thông tin phiên bản
        int buildTimeIndex = attribute.InformationalVersion.IndexOf(prefix);
        if (buildTimeIndex == -1)
            return default;

        // Lấy chuỗi thời gian build từ thuộc tính
        string buildTimeString = attribute.InformationalVersion[(buildTimeIndex + prefix.Length)..];
        if (!DateTime.TryParseExact(buildTimeString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var buildTime))
            return default;

        return buildTime;
    }
}