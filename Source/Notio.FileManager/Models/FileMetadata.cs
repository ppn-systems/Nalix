using System;

namespace Notio.FileManager.Models;

public class FileMetadata
{
    public string OriginalExtension { get; set; } // Phần mở rộng gốc
    public string User { get; set; }             // Người dùng
    public string Password { get; set; }         // Mật khẩu (nếu cần)
    public DateTime CreatedDate { get; set; }    // Ngày tạo
}