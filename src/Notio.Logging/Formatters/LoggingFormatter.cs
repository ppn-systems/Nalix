using Notio.Common.Enums;
using Notio.Common.Logging;
using Notio.Common.Models;
using System;
using System.Text;

namespace Notio.Logging.Formatters;

/// <summary>
/// Lớp LoggingFormatter cung cấp các phương thức để định dạng log đầu ra.
/// </summary>
public class LoggingFormatter(bool color = false) : ILoggingFormatter
{
    private readonly bool _color = color;

    /// <summary>
    /// Instance singleton của <see cref="LoggingFormatter"/> có thể tái sử dụng.
    /// </summary>
    internal static readonly LoggingFormatter Instance = new();

    /// <summary>
    /// Định dạng một thông điệp log với timestamp, cấp độ log, ID sự kiện, thông điệp và ngoại lệ.
    /// </summary>
    /// <param name="logMsg">Thông điệp log cần định dạng.</param>
    /// <returns>Chuỗi định dạng log.</returns>
    /// <example>
    /// var formatter = new LoggingFormatter();
    /// string log = formatter.FormatLog(logEntry);
    /// </example>
    public string FormatLog(LoggingEntry logMsg)
        => FormatLogEntry(logMsg.TimeStamp, logMsg.LogLevel,
            logMsg.EventId, logMsg.Message, logMsg.Exception);

    /// <summary>
    /// Định dạng một thông điệp log tĩnh.
    /// </summary>
    /// <param name="timeStamp">Thời gian tạo log.</param>
    /// <param name="logLevel">Cấp độ log.</param>
    /// <param name="eventId">ID sự kiện.</param>
    /// <param name="message">Thông điệp log.</param>
    /// <param name="exception">Ngoại lệ kèm theo (nếu có).</param>
    /// <returns>Chuỗi định dạng log.</returns>
    /// <example>
    /// string log = LoggingFormatter.FormatLogEntry(TimeStamp.UtcNow, LoggingLevel.Information, new EventId(1), "Sample message", null);
    /// </example>
    public string FormatLogEntry(
        DateTime timeStamp, LoggingLevel logLevel, EventId eventId,
        string message, Exception? exception)
    {
        StringBuilder logBuilder = new();

        LoggingBuilder.BuildLog(logBuilder, timeStamp,
            logLevel, eventId, message, exception, _color);

        return logBuilder.ToString();
    }
}