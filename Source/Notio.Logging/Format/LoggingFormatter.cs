using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Notio.Logging.Format;

/// <summary>
/// Lớp LoggingFormatter cung cấp các phương thức để định dạng log đầu ra.
/// </summary>
public class LoggingFormatter : ILoggingFormatter
{
    private static readonly string[] LogLevelStrings =
    [
        "TRCE", // LoggingLevel.Trace (0)
        "DBUG", // LoggingLevel.Debug (1)
        "INFO", // LoggingLevel.Information (2)
        "WARN", // LoggingLevel.Warning (3)
        "FAIL", // LoggingLevel.Error (4)
        "CRIT", // LoggingLevel.Critical (5)
        "NONE"  // LoggingLevel.None (6)
    ];

    /// <summary>
    /// Instance singleton của <see cref="LoggingFormatter"/> có thể tái sử dụng.
    /// </summary>
    internal static readonly LoggingFormatter Instance = new();

    /// <summary>
    /// Khởi tạo một instance mới của <see cref="LoggingFormatter"/>.
    /// </summary>
    public LoggingFormatter()
    { }

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
    public string FormatLog(LoggingEntry logMsg, DateTime timeStamp)
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
    /// string log = LoggingFormatter.FormatLogEntry(DateTime.UtcNow, LoggingLevel.Information, new EventId(1), "Sample message", null);
    /// </example>
    public static string FormatLogEntry(
        DateTime timeStamp, LoggingLevel logLevel, EventId eventId,
        string message, Exception? exception)
    {
        StringBuilder logBuilder = new();

        BuildLog(logBuilder, timeStamp, logLevel, eventId, message, exception);

        return logBuilder.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetShortLogLevel(LoggingLevel logLevel)
    {
        int index = (int)logLevel;
        if ((uint)index < (uint)LogLevelStrings.Length)
        {
            return LogLevelStrings[index];
        }
        return logLevel.ToString().ToUpperInvariant();
    }

    private static void BuildLog(
        StringBuilder builder,
        in DateTime timeStamp,
        LoggingLevel logLevel,
        in EventId eventId,
        string message,
        Exception? exception)
    {
        // Sử dụng Span để tối ưu string manipulation
        Span<char> dateBuffer = stackalloc char[23]; // yyyy-MM-dd HH:mm:ss.fff

        // Format datetime trực tiếp vào Span
        if (timeStamp.TryFormat(dateBuffer, out int charsWritten, "yyyy-MM-dd HH:mm:ss.fff"))
        {
            builder.Append('[')
                   .Append(dateBuffer[..charsWritten])
                   .Append(']')
                   .Append("\t-\t");
        }

        // Append LoggingLevel
        builder.Append('[')
               .Append(GetShortLogLevel(logLevel))
               .Append(']')
               .Append("\t-\t");

        // Append EventId
        builder.Append('[');
        if (eventId.Name is not null)
        {
            builder.Append(eventId.Id)
                   .Append(':')
                   .Append(eventId.Name);
        }
        else
        {
            builder.Append(eventId.Id);
        }
        builder.Append(']')
               .Append("\t-\t");

        // Append Data
        builder.Append('[')
               .Append(message)
               .Append(']');

        // Append Exception if exists
        if (exception is not null)
        {
            builder.Append("\t-\t")
                   .AppendLine()
                   .Append(exception);
        }
    }
}