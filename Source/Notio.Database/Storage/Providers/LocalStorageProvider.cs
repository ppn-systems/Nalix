using Notio.Common.Exceptions;
using Notio.Database.Helpers;
using Notio.Database.Model;
using Notio.Database.Storage.Interfaces;
using Notio.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Notio.Database.Storage.Providers
{
    /// <summary>
    /// Cung cấp các phương thức để quản lý tệp tin cục bộ, bao gồm tải lên, tải về và xóa.
    /// </summary>
    public class LocalStorageProvider : IStorageProvider
    {
        private readonly string _basePath;
        private readonly StorageConfig _settings;

        /// <summary>
        /// Khởi tạo một thể hiện mới của lớp <see cref="LocalStorageProvider"/>.
        /// </summary>
        /// <param name="settings">Cấu hình lưu trữ.</param>
        public LocalStorageProvider(StorageConfig settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _basePath = _settings.LocalStoragePath;

            // Đảm bảo thư mục tồn tại
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        /// <summary>
        /// Tải lên tệp tin không đồng bộ.
        /// </summary>
        /// <param name="fileStream">Luồng tệp tin.</param>
        /// <param name="fileName">Tên tệp tin.</param>
        /// <returns>Siêu dữ liệu của tệp tin đã tải lên.</returns>
        /// <exception cref="ArgumentException">Ném lỗi khi tên tệp tin rỗng.</exception>
        /// <exception cref="DatabaseException">Ném lỗi khi xảy ra lỗi trong quá trình tải lên.</exception>
        public async Task<FileMetadata> UploadAsync(Stream fileStream, string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileStream);

            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be empty", nameof(fileName));

            try
            {
                // Tạo file ID duy nhất và đường dẫn
                string fileId = Guid.NewGuid().ToString("N");
                string safeFileName = FileHelper.GetSafeFileName(fileName);
                string filePath = GetFilePath(fileId);

                // Tạo thư mục con nếu cần (phân chia file theo ngày)
                string directory = Path.GetDirectoryName(filePath);
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

                NotioLog.Instance.Info($"File uploaded successfully. ID: {fileId}, Name: {safeFileName}");

                return metadata;
            }
            catch (Exception ex)
            {
                NotioLog.Instance.Error($"Error uploading file: {fileName}", ex);
                throw new DatabaseException($"Error uploading file: {fileName}", ex);
            }
        }

        /// <summary>
        /// Tải về tệp tin không đồng bộ.
        /// </summary>
        /// <param name="fileId">ID của tệp tin.</param>
        /// <returns>Luồng của tệp tin đã tải về.</returns>
        /// <exception cref="ArgumentException">Ném lỗi khi ID tệp tin rỗng.</exception>
        /// <exception cref="DatabaseException">Ném lỗi khi xảy ra lỗi trong quá trình tải về.</exception>
        public Task<Stream> DownloadAsync(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentException("File ID cannot be empty", nameof(fileId));

            try
            {
                var filePath = GetFilePath(fileId);
                if (!File.Exists(filePath))
                {
                    NotioLog.Instance.Warn($"File not found: {fileId}");
                    throw new DatabaseException($"File not found: {fileId}");
                }

                // Mở file để đọc
                var stream = File.OpenRead(filePath);
                NotioLog.Instance.Info($"File downloaded successfully. ID: {fileId}");
                return Task.FromResult<Stream>(stream);
            }
            catch (Exception ex) when (ex is not DatabaseException)
            {
                NotioLog.Instance.Error($"Error downloading file: {fileId}", ex);
                throw new DatabaseException($"Error downloading file: {fileId}", ex);
            }
        }

        /// <summary>
        /// Xóa tệp tin không đồng bộ.
        /// </summary>
        /// <param name="fileId">ID của tệp tin.</param>
        /// <returns>Một <see cref="Task"/> đại diện cho thao tác không đồng bộ.</returns>
        /// <exception cref="ArgumentException">Ném lỗi khi ID tệp tin rỗng.</exception>
        /// <exception cref="DatabaseException">Ném lỗi khi xảy ra lỗi trong quá trình xóa.</exception>
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
                    NotioLog.Instance.Info($"File deleted successfully. ID: {fileId}");
                }
                else
                {
                    NotioLog.Instance.Warn($"File not found for deletion: {fileId}");
                }
            }
            catch (Exception ex)
            {
                NotioLog.Instance.Error($"Error deleting file: {fileId}", ex);
                throw new DatabaseException($"Error deleting file: {fileId}", ex);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Lấy đường dẫn tệp tin dựa trên ID tệp tin.
        /// </summary>
        /// <param name="fileId">ID của tệp tin.</param>
        /// <returns>Đường dẫn của tệp tin.</returns>
        private string GetFilePath(string fileId)
        {
            // Tạo cấu trúc thư mục phân cấp theo ngày
            string datePath = DateTime.UtcNow.ToString("yyyy/MM/dd");
            return Path.Combine(_basePath, datePath, fileId);
        }
    }
}