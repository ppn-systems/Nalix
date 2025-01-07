using Notio.Logging.Enums;
using Notio.Logging.Interfaces;
using System;
using System.IO;

namespace Notio.Logging.Engine;

/// <summary>
/// Xây dựng cấu hình logging.
/// </summary>
public class LoggingConfig
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
    /// Khởi tạo một <see cref="LoggingConfig"/> mới.
    /// </summary>
    /// <param name="publisher">Đối tượng <see cref="ILoggingPublisher"/> để xuất bản các thông điệp logging.</param>
    internal LoggingConfig(ILoggingPublisher publisher) => _publisher = publisher;

    /// <summary>
    /// Thêm cấu hình mặc định cho LoggingConfig.
    /// </summary>
    /// <param name="configure">Hành động cấu hình mặc định.</param>
    /// <returns>Đối tượng <see cref="LoggingConfig"/> hiện tại.</returns>
    public LoggingConfig ConfigureDefaults(Func<LoggingConfig, LoggingConfig> configure)
        => configure(this);

    /// <summary>
    /// Thêm mục tiêu logging.
    /// </summary>
    /// <param name="target">Đối tượng <see cref="ILoggingTarget"/> để thêm vào.</param>
    /// <returns>Đối tượng <see cref="LoggingConfig"/> hiện tại.</returns>
    public LoggingConfig AddTarget(ILoggingTarget target)
    {
        this.IsDefaults = false;

        _publisher.AddTarget(target);
        return this;
    }

    /// <summary>
    /// Thiết lập mức độ logging tối thiểu.
    /// </summary>
    /// <param name="level">Mức độ <see cref="LoggingLevel"/> tối thiểu.</param>
    /// <returns>Đối tượng <see cref="LoggingConfig"/> hiện tại.</returns>
    public LoggingConfig SetMinLevel(LoggingLevel level)
    {
        this.IsDefaults = false;

        NotioLog.Instance.MinimumLevel = level;
        return this;
    }

    /// <summary>
    /// Thiết lập đường dẫn thư mục lưu trữ nhật ký.
    /// </summary>
    /// <param name="directory">Đường dẫn thư mục mới.</param>
    /// <returns>Đối tượng <see cref="LoggingConfig"/> hiện tại.</returns>
    public LoggingConfig SetLogDirectory(string directory)
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
    /// <returns>Đối tượng <see cref="LoggingConfig"/> hiện tại.</returns>
    public LoggingConfig SetLogFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Invalid file name.", nameof(fileName));

        this.IsDefaults = false;

        LogFileName = fileName;
        return this;
    }
}