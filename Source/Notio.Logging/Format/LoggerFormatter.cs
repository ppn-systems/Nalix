using Notio.Logging.Extensions;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;

namespace Notio.Logging.Format;

public class LoggerFormatter : ILoggerFormatter
{
    internal static readonly LoggerFormatter Instance = new();

    public LoggerFormatter() { }

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

    public string FormatLog(LogMessage logMsg, DateTime timeStamp)
        => FormatLogEntry(timeStamp, logMsg.LogLevel, logMsg.EventId, logMsg.Message, logMsg.Exception);

    public string FormatLogEntry(
        DateTime timeStamp, LogLevel logLevel, EventId eventId,
        string message, Exception exception)
    {
        const int MaxStackAllocatedBufferLength = 256;
        int estimatedLength = CalculateLogLength(timeStamp, eventId, message);

        Span<char> buffer = estimatedLength <= MaxStackAllocatedBufferLength
            ? stackalloc char[MaxStackAllocatedBufferLength]
            : new char[estimatedLength];

        EfficientStringBuilder logBuilder = new(buffer);
        BuildLog(logBuilder, timeStamp, logLevel, eventId, message, exception);

        return logBuilder.ToString();
    }

    private static int CalculateLogLength(DateTime timeStamp, EventId eventId, string message)
    {
        int length = timeStamp.GetFormattedLength() + 1 + 4 + 2 + 3;

        length += eventId.Name?.Length ?? eventId.Id.GetFormattedLength();
        length += 2 + (message?.Length ?? 0);

        return length;
    }

    private static void BuildLog(
        EfficientStringBuilder logBuilder, DateTime timeStamp, LogLevel logLevel,
        EventId eventId, string message, Exception exception)
    {
        timeStamp.TryFormat(logBuilder.RemainingRawChars, out int charsWritten);
        logBuilder.AppendSpan(charsWritten);

        logBuilder.Append('\t');
        logBuilder.Append(GetShortLogLevel(logLevel));
        logBuilder.Append("]\t[");

        if (eventId.Name is not null)
        {
            logBuilder.Append(eventId.Name);
        }
        else
        {
            eventId.Id.TryFormat(logBuilder.RemainingRawChars, out charsWritten);
            logBuilder.AppendSpan(charsWritten);
        }

        logBuilder.Append("]\t");
        logBuilder.Append(message);

        if (exception is not null)
        {
            logBuilder.Append(Environment.NewLine);
            logBuilder.Append(exception.ToString());
        }
    }
}
