using Notio.Infrastructure.Storage.Exceptions;
using Notio.Infrastructure.Storage.Interfaces;
using Notio.Infrastructure.Storage.Models;
using Notio.Infrastructure.Storage.Validators;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Notio.Infrastructure.Storage;

public class StorageService(
    IStorageProvider storageProvider,
    MediaTypeValidator mediaTypeValidator) : IStorageService
{
    private readonly IStorageProvider _storageProvider = storageProvider;
    private readonly MediaTypeValidator _mediaTypeValidator = mediaTypeValidator;

    public async Task<FileMetadata> UploadFileAsync(Stream fileStream, string fileName)
    {
        try
        {
            if (!_mediaTypeValidator.IsValidFile(fileName))
            {
                throw new StorageException($"File type not allowed: {Path.GetExtension(fileName)}");
            }

            var metadata = await _storageProvider.UploadAsync(fileStream, fileName);
            return metadata;
        }
        catch (Exception ex)
        {
            throw new StorageException("Error uploading file", ex);
        }
    }

    public async Task<Stream> DownloadFileAsync(string fileId)
    {
        try
        {
            return await _storageProvider.DownloadAsync(fileId);
        }
        catch (Exception ex)
        {
            throw new StorageException("Error downloading file", ex);
        }
    }

    public async Task DeleteFileAsync(string fileId)
    {
        try
        {
            await _storageProvider.DeleteAsync(fileId);
        }
        catch (Exception ex)
        {
            throw new StorageException("Error deleting file", ex);
        }
    }
}