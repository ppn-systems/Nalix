using System;
using System.IO;

namespace Notio.Shared;

/// <summary>
/// Lớp cấu hình các đường dẫn mặc định cho ứng dụng.
/// </summary>
public static class DefaultDirectories
{
    /// <summary>
    /// Đường dẫn gốc của ứng dụng.
    /// </summary>
    public static readonly string BasePath = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    /// <summary>
    /// Đường dẫn lưu trữ file log.
    /// </summary>
    public static readonly string LogsPath = Path.Combine(BasePath, "Logs");

    /// <summary>
    /// Đường dẫn lưu trữ dữ liệu.
    /// </summary>
    public static readonly string DataPath = Path.Combine(BasePath, "Data");

    /// <summary>
    /// Đường dẫn lưu trữ tệp tạm thời.
    /// </summary>
    public static readonly string TempPath = Path.Combine(DataPath, "Temp");

    /// <summary>
    /// Đường dẫn lưu trữ tệp số liệu.
    /// </summary>
    public static readonly string MetricPath = Path.Combine(DataPath, "Metrics");

    /// <summary>
    /// Khởi tạo các thư mục mặc định.
    /// </summary>
    static DefaultDirectories()
    {
        EnsureDirectoriesExist(LogsPath, DataPath);
    }

    /// <summary>
    /// Đảm bảo các thư mục tồn tại, nếu chưa thì tạo mới.
    /// </summary>
    /// <param name="paths">Danh sách các đường dẫn cần kiểm tra.</param>
    private static void EnsureDirectoriesExist(params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to create folders: {path}", ex);
            }
        }
    }
}