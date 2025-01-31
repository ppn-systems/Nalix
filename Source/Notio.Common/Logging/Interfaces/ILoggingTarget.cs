using Notio.Common.Models;

namespace Notio.Common.Logging.Interfaces;

/// <summary>
/// Định nghĩa giao diện cho mục tiêu xử lý nhật ký.
/// </summary>
public interface ILoggingTarget
{
    /// <summary>
    /// Gửi một thông điệp log đến mục tiêu xử lý.
    /// </summary>
    /// <param name="logMessage">Đối tượng thông điệp log.</param>
    void Publish(LoggingEntry logMessage);
}