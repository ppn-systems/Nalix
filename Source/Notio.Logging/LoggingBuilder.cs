using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;

namespace Notio.Logging;

/// <summary>
/// Xây dựng cấu hình logging.
/// </summary>
public class LoggingBuilder
{
    private readonly ILoggingPublisher _publisher;

    /// <summary>
    /// Cho biết có sử dụng cấu hình mặc định hay không.
    /// </summary>
    internal bool IsDefaults { get; private set; } = true;

    /// <summary>
    /// Khởi tạo một <see cref="LoggingBuilder"/> mới.
    /// </summary>
    /// <param name="publisher">Đối tượng <see cref="ILoggingPublisher"/> để xuất bản các thông điệp logging.</param>
    internal LoggingBuilder(ILoggingPublisher publisher)
    {
        _publisher = publisher;
    }

    /// <summary>
    /// Thêm mục tiêu logging.
    /// </summary>
    /// <param name="target">Đối tượng <see cref="ILoggingTarget"/> để thêm vào.</param>
    /// <returns>Đối tượng <see cref="LoggingBuilder"/> hiện tại.</returns>
    public LoggingBuilder AddTarget(ILoggingTarget target)
    {
        if (IsDefaults)
        {
            this.IsDefaults = false;
        }

        _publisher.AddTarget(target);
        return this;
    }

    /// <summary>
    /// Thiết lập mức độ logging tối thiểu.
    /// </summary>
    /// <param name="level">Mức độ <see cref="LogLevel"/> tối thiểu.</param>
    /// <returns>Đối tượng <see cref="LoggingBuilder"/> hiện tại.</returns>
    public LoggingBuilder SetMinLevel(LogLevel level)
    {
        NotioLog.Instance.MinimumLevel = level;
        return this;
    }
}