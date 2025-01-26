using Notio.FileStorage.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Notio.FileStorage.Interfaces;

public interface IFileStorageAsync
{
    Task UploadAsync(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original");

    Task<IFile> DownloadAsync(string fileName, string format = "original");

    Task<string> GetFileUriAsync(string fileName, string format = "original");

    Task<bool> FileExistsAsync(string fileName, string format = "original");

    Stream GetStream(string fileName, IEnumerable<FileMeta> metaInfo, string format = "original");

    Task DeleteAsync(string fileName);
}