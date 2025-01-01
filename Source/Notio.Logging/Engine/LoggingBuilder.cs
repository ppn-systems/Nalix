using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System.IO;
using System;

namespace Notio.Logging.Engine;

/// <summary>
/// Xây dựng cấu hình logging.
/// </summary>
public class LoggingBuilder
{
    private static readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    private readonly ILoggingPublisher _publisher;

    /// <summary>
    /// Cho biết có sử dụng cấu hình mặc định hay không.
    /// </summary>
    internal bool IsDefaults { get; private set; } = true;

    /// <summary>
    /// Đường dẫn thư mục lưu trữ nhật ký.
    /// </summary>
    public string LogDirectory { get; private set; } = Path.Combine(_baseDirectory, "Logs");

    /// <summary>
    /// Tên file lưu trữ nhật ký mặc định.
    /// </summary>
    public string LogFileName { get; private set; } = "Notio";

    /// <summary>
    /// Khởi tạo một <see cref="LoggingBuilder"/> mới.
    /// </summary>
    /// <param name="publisher">Đối tượng <see cref="ILoggingPublisher"/> để xuất bản các thông điệp logging.</param>
    internal LoggingBuilder(ILoggingPublisher publisher) => _publisher = publisher;
    
    /// <summary>
    /// Thêm mục tiêu logging.
    /// </summary>
    /// <param name="target">Đối tượng <see cref="ILoggingTarget"/> để thêm vào.</param>
    /// <returns>Đối tượng <see cref="LoggingBuilder"/> hiện tại.</returns>
    public LoggingBuilder AddTarget(ILoggingTarget target)
    {
        this.IsDefaults = false;

        _publisher.AddTarget(target);
        return this;
    }

    /// <summary>
    /// Thiết lập mức độ logging tối thiểu.
    /// </summary>
    /// <param name="level">Mức độ <see cref="LoggingLevel"/> tối thiểu.</param>
    /// <returns>Đối tượng <see cref="LoggingBuilder"/> hiện tại.</returns>
    public LoggingBuilder SetMinLevel(LoggingLevel level)
    {
        this.IsDefaults = false;

        NotioLog.Instance.MinimumLevel = level;
        return this;
    }

    /// <summary>
    /// Thiết lập đường dẫn thư mục lưu trữ nhật ký.
    /// </summary>
    /// <param name="directory">Đường dẫn thư mục mới.</param>
    /// <returns>Đối tượng <see cref="LoggingBuilder"/> hiện tại.</returns>
    public LoggingBuilder SetLogDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Invalid directory.", nameof(directory));
        
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        this.IsDefaults = false;

        LogDirectory = directory;
        return this;
    }

    /// <summary>
    /// Thiết lập tên file lưu trữ nhật ký.
    /// </summary>
    /// <param name="fileName">Tên file mới.</param>
    /// <returns>Đối tượng <see cref="LoggingBuilder"/> hiện tại.</returns>
    public LoggingBuilder SetLogFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Invalid file name.", nameof(fileName));

        this.IsDefaults = false;

        LogFileName = fileName;
        return this;
    }
}