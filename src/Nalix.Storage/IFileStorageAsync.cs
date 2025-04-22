using Nalix.Storage.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nalix.Storage;

/// <summary>
/// Defines the asynchronous contract for file storage operations.
/// </summary>
public interface IFileStorageAsync : IStreamProvider
{
    /// <summary>
    /// Uploads a file to the storage asynchronously.
    /// </summary>
    /// <param name="fileName">The name of the file to upload.</param>
    /// <param name="data">The binary data of the file.</param>
    /// <param name="metaInfo">The metadata associated with the file.</param>
    /// <param name="format">The format to store the file (default is "original").</param>
    Task UploadAsync(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original");

    /// <summary>
    /// Downloads a file from the storage asynchronously.
    /// </summary>
    /// <param name="fileName">The name of the file to download.</param>
    /// <param name="format">The format of the file to retrieve (default is "original").</param>
    /// <returns>An instance of <see cref="IFile"/> containing the file's data and name.</returns>
    Task<IFile> DownloadAsync(string fileName, string format = "original");

    /// <summary>
    /// Deletes a file from the storage asynchronously.
    /// </summary>
    /// <param name="fileName">The name of the file to delete.</param>
    Task DeleteAsync(string fileName);

    /// <summary>
    /// Retrieves the URI of a stored file asynchronously.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="format">The format of the file (default is "original").</param>
    /// <returns>The URI of the file.</returns>
    Task<string> GetFileUriAsync(string fileName, string format = "original");

    /// <summary>
    /// Checks if a file exists in the storage asynchronously.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="format">The format of the file to check (default is "original").</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    Task<bool> FileExistsAsync(string fileName, string format = "original");
}