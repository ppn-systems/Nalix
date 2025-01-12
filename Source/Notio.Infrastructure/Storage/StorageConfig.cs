namespace Notio.Infrastructure.Storage;

public class StorageConfig
{
    public string LocalStoragePath { get; set; }
    public string[] AllowedFileExtensions { get; set; }
    public string CloudStorageConnectionString { get; set; }
}