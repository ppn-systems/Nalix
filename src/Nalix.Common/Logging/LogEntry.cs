namespace Nalix.Common.Logging;

/// <summary>
/// Represents a single log entry recorded by the logging system.
/// </summary>
/// <param name="level">
/// The severity level of the log entry.
/// </param>
/// <param name="eventId">
/// The identifier associated with the event that produced the log entry.
/// </param>
/// <param name="message">
/// The message content describing the log event.
/// </param>
/// <param name="exception">
/// The exception related to the log entry, if any.
/// </param>
public readonly struct LogEntry(
    LogLevel level,
    EventId eventId,
    System.String message,
    System.Exception? exception = null)
{
    /// <summary>
    /// Gets the severity level of the log entry.
    /// </summary>
    public readonly LogLevel LogLevel = level;

    /// <summary>
    /// Gets the identifier associated with the event that produced the log entry.
    /// </summary>
    public readonly EventId EventId = eventId;

    /// <summary>
    /// Gets the content of the log message.
    /// </summary>
    public readonly System.String Message = message;

    /// <summary>
    /// Gets the exception associated with the log entry, if any.
    /// </summary>
    public readonly System.Exception? Exception = exception;

    /// <summary>
    /// Gets the UTC timestamp indicating when the log entry was created.
    /// </summary>
    public readonly System.DateTime TimeStamp = System.DateTime.UtcNow;
}
