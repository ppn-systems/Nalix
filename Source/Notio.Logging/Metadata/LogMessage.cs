using System;

namespace Notio.Logging.Metadata;

public readonly struct LogMessage
{
    public readonly string Message;
    public readonly LogLevel LogLevel;
    public readonly EventId EventId;
    public readonly Exception Exception;

    internal LogMessage(LogLevel level, EventId eventId, string message, Exception exception = null)
    {
        Message = message;
        LogLevel = level;
        EventId = eventId;
        Exception = exception;
    }
}