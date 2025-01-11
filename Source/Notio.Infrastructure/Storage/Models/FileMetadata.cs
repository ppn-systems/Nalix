using System;

namespace Notio.Infrastructure.Storage.Models;

public class FileMetadata
{
    public required string Id { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
}
