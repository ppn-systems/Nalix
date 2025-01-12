using System;

namespace Notio.Infrastructure.Storage.Models;

public class FileMetadata
{
    public string Id { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
}