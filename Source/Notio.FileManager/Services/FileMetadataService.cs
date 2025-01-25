using System;
using System.IO;
using Notio.FileManager.Models;

namespace Notio.FileManager.Services;

public static class FileMetadataService
{
    public static void WriteMetadata(string filePath, FileMetadata metadata, string content)
    {
        // Chuyển metadata thành chuỗi JSON
        string metadataLine = $"{metadata.OriginalExtension}|{metadata.User}|{metadata.Password}|{metadata.CreatedDate:yyyy-MM-dd HH:mm:ss}";

        // Ghi metadata vào line 0 và nội dung vào các dòng tiếp theo
        using var writer = new StreamWriter(filePath);
        writer.WriteLine(metadataLine); // Line 0: Metadata
        writer.Write(content);         // Các dòng tiếp theo: Nội dung gốc
    }

    public static (FileMetadata metadata, string content) ReadMetadata(string filePath)
    {
        using var reader = new StreamReader(filePath);
        // Đọc line 0: Metadata
        string metadataLine = reader.ReadLine();
        var metadataParts = metadataLine.Split('|');

        var metadata = new FileMetadata
        {
            OriginalExtension = metadataParts[0],
            User = metadataParts[1],
            Password = metadataParts[2],
            CreatedDate = DateTime.Parse(metadataParts[3])
        };

        // Đọc nội dung gốc từ các dòng tiếp theo
        string content = reader.ReadToEnd();

        return (metadata, content);
    }
}