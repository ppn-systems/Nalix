using Notio.Logging.Format;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;

namespace Notio.Logging.Sinks;

/// <summary>
/// Lớp ConsoleLogSinks cung cấp khả năng xuất thông điệp nhật ký ra console với màu sắc tương ứng với mức độ log.
/// </summary>
/// <remarks>
/// Khởi tạo đối tượng ConsoleLogSinks với định dạng log cụ thể.
/// </remarks>
/// <param name="loggerFormatter">Đối tượng thực hiện định dạng log.</param>
public sealed class ConsoleLogSinks(ILoggerFormatter loggerFormatter) : ILoggerSinks
{
    private readonly ILoggerFormatter _loggerFormatter = loggerFormatter;

    public ConsoleLogSinks() : this(new LoggerFormatter()) { }

    /// <summary>
    /// Xuất thông điệp log ra console.
    /// </summary>
    /// <param name="logMessage">Thông điệp log cần xuất.</param>
    public void Publish(LogMessage logMessage)
    {
        try
        {
            // Lấy màu tương ứng với mức log
            SetForegroundColor(logMessage.LogLevel);
            Console.WriteLine(_loggerFormatter.FormatLog(logMessage, DateTime.Now));
        }
        finally
        {
            Console.ResetColor(); // Đảm bảo reset màu sau khi in
        }
    }

    /// <summary>
    /// Đặt màu sắc cho mức độ log.
    /// </summary>
    /// <param name="level">Mức độ log cần đặt màu sắc.</param>
    private static void SetForegroundColor(LogLevel level)
    {
        var color = level switch
        {
            LogLevel.Trace => ConsoleColor.Gray,
            LogLevel.Information => ConsoleColor.White,
            LogLevel.Debug => ConsoleColor.Green,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Magenta,
            LogLevel.Critical => ConsoleColor.Red,
            LogLevel.None => ConsoleColor.Cyan,
            _ => ConsoleColor.White, // Mặc định
        };
        Console.ForegroundColor = color;
    }
}