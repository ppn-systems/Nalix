// LocalStorageProvider.cs
using Notio.Infrastructure.Storage.Exceptions;
using Notio.Infrastructure.Storage.Helpers;
using Notio.Infrastructure.Storage.Interfaces;
using Notio.Infrastructure.Storage.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Notio.Infrastructure.Storage.Providers
{
    public class LocalStorageProvider : IStorageProvider
    {
        private readonly string _basePath;
        private readonly StorageConfig _settings;

        public LocalStorageProvider(
            StorageConfig settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _basePath = _settings.LocalStoragePath;

            // Đảm bảo thư mục tồn tại
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        public async Task<FileMetadata> UploadAsync(Stream fileStream, string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileStream);

            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be empty", nameof(fileName));

            try
            {
                // Tạo file ID duy nhất và đường dẫn
                string? fileId = Guid.NewGuid().ToString("N");
                string? safeFileName = FileHelper.GetSafeFileName(fileName);
                string? filePath = GetFilePath(fileId);

                // Tạo thư mục con nếu cần (phân chia file theo ngày)
                string? directory = Path.GetDirectoryName(filePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Lưu file
                using (var localFileStream = new FileStream(filePath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(localFileStream);
                }

                // Tạo metadata
                var metadata = new FileMetadata
                {
                    Id = fileId,
                    FileName = safeFileName,
                    ContentType = FileHelper.GetContentType(fileName),
                    Size = fileStream.Length,
                    CreatedAt = DateTime.UtcNow
                };

                //_logger.LogInformation("File uploaded successfully. ID: {FileId}, Name: {FileName}", fileId, safeFileName);

                return metadata;
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error uploading file: {FileName}", fileName);
                throw new StorageException($"Error uploading file: {fileName}", ex);
            }
        }

        public Task<Stream> DownloadAsync(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be empty", nameof(fileId));

            try
            {
                var filePath = GetFilePath(fileId);
                if (!File.Exists(filePath))
                {
                    //_logger.LogWarning("File not found: {FileId}", fileId);
                    throw new StorageException($"File not found: {fileId}");
                }

                // Mở file để đọc
                var stream = File.OpenRead(filePath);
                //_logger.LogInformation("File downloaded successfully. ID: {FileId}", fileId);
                return Task.FromResult<Stream>(stream);
            }
            catch (Exception ex) when (ex is not StorageException)
            {
                //_logger.LogError(ex, "Error downloading file: {FileId}", fileId);
                throw new StorageException($"Error downloading file: {fileId}", ex);
            }
        }

        public Task DeleteAsync(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be empty", nameof(fileId));

            try
            {
                var filePath = GetFilePath(fileId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    //_logger.LogInformation("File deleted successfully. ID: {FileId}", fileId);
                }
                else
                {
                    //_logger.LogWarning("File not found for deletion: {FileId}", fileId);
                }
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error deleting file: {FileId}", fileId);
                throw new StorageException($"Error deleting file: {fileId}", ex);
            }

            return Task.CompletedTask;
        }

        private string GetFilePath(string fileId)
        {
            // Tạo cấu trúc thư mục phân cấp theo ngày
            var datePath = DateTime.UtcNow.ToString("yyyy/MM/dd");
            return Path.Combine(_basePath, datePath, fileId);
        }
    }
}