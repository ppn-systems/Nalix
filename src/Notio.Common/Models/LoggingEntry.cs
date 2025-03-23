using Notio.Common.Enums;
using System;

namespace Notio.Common.Models;

/// <summary>
/// Represents a log entry in the logging system.
/// </summary>
/// <param name="level">The log level of the entry.</param>
/// <param name="eventId">The event ID associated with the log entry.</param>
/// <param name="message">The content of the log message.</param>
/// <param name="exception">The accompanying exception (if any).</param>
public readonly struct LoggingEntry(
    LoggingLevel level, EventId eventId, string message, Exception exception = null)
{
    /// <summary>
    /// The timestamp of the log entry.
    /// </summary>
    public readonly DateTime TimeStamp = DateTime.UtcNow;

    /// <summary>
    /// The content of the log message.
    /// </summary>
    public readonly string Message = message;

    /// <summary>
    /// The log level of the entry.
    /// </summary>
    public readonly LoggingLevel LogLevel = level;

    /// <summary>
    /// The event ID associated with the log entry.
    /// </summary>
    public readonly EventId EventId = eventId;

    /// <summary>
    /// The accompanying exception, if any.
    /// </summary>
    public readonly Exception Exception = exception;
}
