using Notio.Infrastructure.Storage.Models;
using System.IO;
using System.Threading.Tasks;

namespace Notio.Infrastructure.Storage.Interfaces;

public interface IStorageProvider
{
    Task<FileMetadata> UploadAsync(Stream fileStream, string fileName);
    Task<Stream> DownloadAsync(string fileId);
    Task DeleteAsync(string fileId);
}