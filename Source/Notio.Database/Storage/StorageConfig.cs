using Notio.Shared.Configuration;

namespace Notio.Database.Storage;

/// <summary>
/// Đại diện cho cấu hình lưu trữ bao gồm đường dẫn lưu trữ cục bộ,
/// các phần mở rộng tệp được phép và chuỗi kết nối lưu trữ đám mây.
/// </summary>
public class StorageConfig : ConfigurationBinder
{
    /// <summary>
    /// Đường dẫn lưu trữ cục bộ.
    /// </summary>
    public string LocalStoragePath { get; set; }

    /// <summary>
    /// Chuỗi kết nối lưu trữ đám mây.
    /// </summary>
    public string CloudStorageConnectionString { get; set; }

    /// <summary>
    /// Các phần mở rộng tệp được phép lưu trữ.
    /// </summary>
    [ConfigurationIgnore]
    public string[] AllowedFileExtensions { get; set; }
}