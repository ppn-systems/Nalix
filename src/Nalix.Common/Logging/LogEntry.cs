namespace Nalix.Common.Logging;

/// <summary>
/// Represents a log entry in the logging system.
/// </summary>
/// <param name="level">The log level of the entry.</param>
/// <param name="eventId">The event Number associated with the log entry.</param>
/// <param name="message">The content of the log message.</param>
/// <param name="exception">The accompanying exception (if any).</param>
public readonly struct LogEntry(
    LogLevel level,
    EventId eventId,
    System.String message,
    System.Exception exception = null)
{
    /// <summary>
    /// The log level of the entry.
    /// </summary>
    public readonly LogLevel LogLevel = level;

    /// <summary>
    /// The event Number associated with the log entry.
    /// </summary>
    public readonly EventId EventId = eventId;

    /// <summary>
    /// The content of the log message.
    /// </summary>
    public readonly System.String Message = message;

    /// <summary>
    /// The accompanying exception, if any.
    /// </summary>
    public readonly System.Exception Exception = exception;

    /// <summary>
    /// The timestamp of the log entry.
    /// </summary>
    public readonly System.DateTime TimeStamp = System.DateTime.UtcNow;
}
