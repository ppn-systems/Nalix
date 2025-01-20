using System;

namespace Notio.Database.Entities;

/// <summary>
/// Đại diện cho siêu dữ liệu của tệp tin trong cơ sở dữ liệu lưu trữ.
/// </summary>
public class FileMetadata
{
    /// <summary>
    /// Định danh duy nhất của tệp tin.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Tên tệp tin.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Loại nội dung của tệp tin (ví dụ: "image/png").
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Kích thước của tệp tin tính bằng byte.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Thời điểm tạo tệp tin.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}