using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;
using System.Text;

namespace Notio.Logging.Format;

/// <summary>
/// Lớp LoggingFormatter cung cấp các phương thức để định dạng log đầu ra.
/// </summary>
public class LoggingFormatter : ILoggingFormatter
{
    private const string LogSeparator = " - ";
    /// <summary>
    /// Instance singleton của <see cref="LoggingFormatter"/> có thể tái sử dụng.
    /// </summary>
    internal static readonly LoggingFormatter Instance = new();

    /// <summary>
    /// Khởi tạo một instance mới của <see cref="LoggingFormatter"/>.
    /// </summary>
    public LoggingFormatter() { }

    /// <summary>
    /// Định dạng một thông điệp log với timestamp, cấp độ log, ID sự kiện, thông điệp và ngoại lệ.
    /// </summary>
    /// <param name="logMsg">Thông điệp log cần định dạng.</param>
    /// <param name="timeStamp">Thời gian tạo log.</param>
    /// <returns>Chuỗi định dạng log.</returns>
    /// <example>
    /// var formatter = new LoggingFormatter();
    /// string log = formatter.FormatLog(logEntry, DateTime.UtcNow);
    /// </example>
    public string FormatLog(LogEntry logMsg, DateTime timeStamp)
        => FormatLogEntry(timeStamp, logMsg.LogLevel, logMsg.EventId, logMsg.Message, logMsg.Exception);

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
    /// string log = LoggingFormatter.FormatLogEntry(DateTime.UtcNow, LogLevel.Information, new EventId(1), "Sample message", null);
    /// </example>
    public static string FormatLogEntry(
        DateTime timeStamp, LogLevel logLevel, EventId eventId,
        string message, Exception? exception)
    {
        StringBuilder logBuilder = new();

        BuildLog(logBuilder, timeStamp, logLevel, eventId, message, exception);

        return logBuilder.ToString();
    }

    private static string GetShortLogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "TRCE",
        LogLevel.Debug => "DBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "FAIL",
        LogLevel.Critical => "CRIT",
        LogLevel.None => "NONE",
        _ => logLevel.ToString().ToUpperInvariant(),
    };

    private static void BuildLog(
        StringBuilder logBuilder, DateTime timeStamp, LogLevel logLevel,
        EventId eventId, string message, Exception? exception)
    {
        logBuilder.AppendFormat("{0:yyyy-MM-dd HH:mm:ss.fff}", timeStamp);
        logBuilder.Append(LogSeparator);
        logBuilder.Append(GetShortLogLevel(logLevel));
        logBuilder.Append(LogSeparator);

        if (eventId.Name is not null)
        {
            logBuilder.Append(eventId.Name);
        }
        else
        {
            logBuilder.Append(eventId.Id);
        }

        logBuilder.Append(LogSeparator);
        logBuilder.Append(message);

        if (exception is not null)
        {
            logBuilder.Append(Environment.NewLine);
            logBuilder.Append(exception);
        }
    }
}
