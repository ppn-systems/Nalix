using Notio.Common.Enums;
using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Logging.Format;
using System;

namespace Notio.Logging.Targets;

/// <summary>
/// Lớp ConsoleTarget cung cấp khả năng xuất thông điệp nhật ký ra console với màu sắc tương ứng với mức độ log.
/// </summary>
/// <remarks>
/// Khởi tạo đối tượng ConsoleTarget với định dạng log cụ thể.
/// </remarks>
/// <param name="loggerFormatter">Đối tượng thực hiện định dạng log.</param>
public sealed class ConsoleTarget(ILoggingFormatter loggerFormatter) : ILoggingTarget
{
    private readonly ILoggingFormatter _loggerFormatter = loggerFormatter;

    public ConsoleTarget() : this(new LoggingFormatter(true))
    {
    }

    /// <summary>
    /// Xuất thông điệp log ra console.
    /// </summary>
    /// <param name="logMessage">Thông điệp log cần xuất.</param>
    public void Publish(LoggingEntry logMessage)
        => Console.WriteLine(_loggerFormatter.FormatLog(logMessage, DateTime.Now));
}