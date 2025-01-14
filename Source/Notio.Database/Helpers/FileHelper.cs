using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Notio.Database.Helpers;

/// <summary>
/// Cung cấp các phương thức trợ giúp liên quan đến tệp.
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// Lấy tên tệp tin an toàn bằng cách loại bỏ các ký tự không hợp lệ.
    /// </summary>
    /// <param name="fileName">Tên tệp tin ban đầu.</param>
    /// <returns>Tên tệp tin an toàn.</returns>
    public static string GetSafeFileName(string fileName)
        => new(fileName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());

    /// <summary>
    /// Lấy loại nội dung (MIME type) của tệp tin dựa trên phần mở rộng.
    /// </summary>
    /// <param name="fileName">Tên tệp tin.</param>
    /// <returns>Loại nội dung của tệp tin.</returns>
    public static string GetContentType(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return GetMimeType(extension);
    }

    /// <summary>
    /// Từ điển chứa các phần mở rộng tệp tin và loại nội dung tương ứng.
    /// </summary>
    private static readonly Dictionary<string, string> MimeTypes = new()
    {
        // Văn bản và tài liệu
        {".txt", "text/plain"},
        {".csv", "text/csv"},
        {".html", "text/html"},
        {".htm", "text/html"},
        {".pdf", "application/pdf"},
        {".doc", "application/msword"},
        {".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"},
        {".xls", "application/vnd.ms-excel"},
        {".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},
        {".ppt", "application/vnd.ms-powerpoint"},
        {".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"},

        // Hình ảnh
        {".jpg", "image/jpeg"},
        {".jpeg", "image/jpeg"},
        {".png", "image/png"},
        {".gif", "image/gif"},
        {".bmp", "image/bmp"},
        {".webp", "image/webp"},
        {".svg", "image/svg+xml"},
        {".ico", "image/x-icon"},

        // Âm thanh
        {".mp3", "audio/mpeg"},
        {".wav", "audio/wav"},
        {".ogg", "audio/ogg"},
        {".m4a", "audio/mp4"},
        {".flac", "audio/flac"},

        // Video
        {".mp4", "video/mp4"},
        {".avi", "video/x-msvideo"},
        {".mov", "video/quicktime"},
        {".mkv", "video/x-matroska"},
        {".wmv", "video/x-ms-wmv"},
        {".flv", "video/x-flv"},
        {".webm", "video/webm"},

        // Lưu trữ
        {".zip", "application/zip"},
        {".rar", "application/vnd.rar"},
        {".7z", "application/x-7z-compressed"},
        {".tar", "application/x-tar"},
        {".gz", "application/gzip"},

        // Mã nguồn
        {".json", "application/json"},
        {".xml", "application/xml"},
        {".js", "application/javascript"},
        {".css", "text/css"},
        {".php", "application/x-httpd-php"},
        {".c", "text/x-c"},
        {".cpp", "text/x-c"},
        {".cs", "text/x-csharp"},
        {".java", "text/x-java-source"},
        {".py", "text/x-python"},
        {".rb", "application/x-ruby"},
        {".go", "text/x-go"},
        {".ts", "application/typescript"},

        // Loại khác
        {".exe", "application/octet-stream"},
        {".dll", "application/octet-stream"},
        {".iso", "application/x-iso9660-image"},
        {".eot", "application/vnd.ms-fontobject"},
        {".ttf", "font/ttf"},
        {".otf", "font/otf"},
        {".woff", "font/woff"},
        {".woff2", "font/woff2"},
        {".unknown", "application/octet-stream"}
    };

    /// <summary>
    /// Lấy loại nội dung (MIME type) dựa trên phần mở rộng tệp tin.
    /// </summary>
    /// <param name="extension">Phần mở rộng của tệp tin.</param>
    /// <returns>Loại nội dung tương ứng với phần mở rộng.</returns>
    private static string GetMimeType(string extension)
        => MimeTypes.TryGetValue(extension, out string mimeType) ? mimeType : "application/octet-stream";
}