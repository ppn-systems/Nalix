using Notio.Common.Logging.Interfaces;
using Notio.Common.Models;
using Notio.Logging.Formatters;
using System;

namespace Notio.Logging.Targets;

/// <summary>
/// Lớp ConsoleLoggingTarget cung cấp khả năng xuất thông điệp nhật ký ra console với màu sắc tương ứng với mức độ log.
/// </summary>
/// <remarks>
/// Khởi tạo đối tượng ConsoleLoggingTarget với định dạng log cụ thể.
/// </remarks>
/// <param name="loggerFormatter">Đối tượng thực hiện định dạng log.</param>
public sealed class ConsoleLoggingTarget(ILoggingFormatter loggerFormatter) : ILoggingTarget
{
    private readonly ILoggingFormatter _loggerFormatter = loggerFormatter;

    public ConsoleLoggingTarget() : this(new LoggingFormatter(true))
    {
    }

    /// <summary>
    /// Xuất thông điệp log ra console.
    /// </summary>
    /// <param name="logMessage">Thông điệp log cần xuất.</param>
    public void Publish(LoggingEntry logMessage)
        => Console.WriteLine(_loggerFormatter.FormatLog(logMessage));
}