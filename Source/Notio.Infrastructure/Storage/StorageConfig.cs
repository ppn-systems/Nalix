namespace Notio.Infrastructure.Storage;

public class StorageConfig
{
    public required string LocalStoragePath { get; set; }
    public required string[] AllowedFileExtensions { get; set; }
    public required string CloudStorageConnectionString { get; set; }
}