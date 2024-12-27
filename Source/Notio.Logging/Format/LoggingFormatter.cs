using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;
using System.Text;

namespace Notio.Logging.Format;

public class LoggingFormatter : ILoggingFormatter
{
    internal static readonly LoggingFormatter Instance = new();

    public LoggingFormatter() { }

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

    public string FormatLog(LogEntry logMsg, DateTime timeStamp)
        => FormatLogEntry(timeStamp, logMsg.LogLevel, logMsg.EventId, logMsg.Message, logMsg.Exception);

    public static string FormatLogEntry(
        DateTime timeStamp, LogLevel logLevel, EventId eventId,
        string message, Exception? exception)
    {
        StringBuilder logBuilder = new();

        BuildLog(logBuilder, timeStamp, logLevel, eventId, message, exception);

        return logBuilder.ToString();
    }

    private static void BuildLog(
        StringBuilder logBuilder, DateTime timeStamp, LogLevel logLevel,
        EventId eventId, string message, Exception? exception)
    {
        logBuilder.AppendFormat("{0:yyyy-MM-dd HH:mm:ss.fff}", timeStamp);
        logBuilder.Append('\t');
        logBuilder.Append(GetShortLogLevel(logLevel));
        logBuilder.Append("]\t[");

        if (eventId.Name is not null)
        {
            logBuilder.Append(eventId.Name);
        }
        else
        {
            logBuilder.Append(eventId.Id);
        }

        logBuilder.Append("]\t");
        logBuilder.Append(message);

        if (exception is not null)
        {
            logBuilder.Append(Environment.NewLine);
            logBuilder.Append(exception);
        }
    }
}
