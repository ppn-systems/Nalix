using System;

namespace Notio.Common.Logging;

/// <summary>
/// Defines the contract for a logging system.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Logs metadata information.
    /// </summary>
    void Meta(string message, EventId? eventId = null);

    /// <summary>
    /// Logs trace information.
    /// </summary>
    void Trace(string message, EventId? eventId = null);

    /// <summary>
    /// Logs debug information.
    /// </summary>
    void Debug(string message, EventId? eventId = null, string memberName = "");

    /// <summary>
    /// Logs debug information for a specific class.
    /// </summary>
    void Debug<TClass>(string message, EventId? eventId = null, string memberName = "")
        where TClass : class;

    /// <summary>
    /// Logs information.
    /// </summary>
    void Info(string format, params object[] args);

    /// <summary>
    /// Logs information.
    /// </summary>
    void Info(string message, EventId? eventId = null);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    void Warn(string message, EventId? eventId = null);

    /// <summary>
    /// Logs an error with message.
    /// </summary>
    public void Error(string message, EventId? eventId = null);

    /// <summary>
    /// Logs an error with exception.
    /// </summary>
    void Error(Exception exception, EventId? eventId = null);

    /// <summary>
    /// Logs an error with message and exception.
    /// </summary>
    void Error(string message, Exception exception, EventId? eventId = null);

    /// <summary>
    /// Logs a critical error message.
    /// </summary>
    void Fatal(string message, EventId? eventId = null);

    /// <summary>
    /// Logs a critical error with message and exception.
    /// </summary>
    void Fatal(string message, Exception exception, EventId? eventId = null);
}
