using Notio.Common.Models;
using System;

namespace Notio.Common.Logging;

/// <summary>
/// Defines the contract for a logging system that provides hierarchical logging levels.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Logs metadata information useful for system configuration and setup.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Meta(string message);

    /// <summary>
    /// Logs metadata information with optional event identifier.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Meta(string message, EventId? eventId = null);

    /// <summary>
    /// Logs trace-level information for detailed diagnostics.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Trace(string message);

    /// <summary>
    /// Logs trace-level information with optional event identifier.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Trace(string message, EventId? eventId = null);

    /// <summary>
    /// Logs debug information for development and troubleshooting.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Debug(string message);

    /// <summary>
    /// Logs debug information with optional event identifier.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Debug(string message, EventId? eventId = null);

    /// <summary>
    /// Logs debug information for a specific class type.
    /// </summary>
    /// <typeparam name="TClass">The class type for context.</typeparam>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    /// <param name="memberName">Optional member name where logging occurs.</param>
    void Debug<TClass>(string message, EventId? eventId = null, string memberName = "")
        where TClass : class;

    /// <summary>
    /// Logs information with format and arguments.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The arguments to format.</param>
    void Info(string format, params object[] args);

    /// <summary>
    /// Logs information about normal application flow.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Info(string message);

    /// <summary>
    /// Logs information with optional event identifier.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Info(string message, EventId? eventId = null);

    /// <summary>
    /// Logs a warning about potential issues.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    void Warn(string message);

    /// <summary>
    /// Logs a warning with optional event identifier.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Warn(string message, EventId? eventId = null);

    /// <summary>
    /// Logs an error message about handled exceptions.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    void Error(string message);

    /// <summary>
    /// Logs an error message with optional event identifier.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Error(string message, EventId? eventId = null);

    /// <summary>
    /// Logs an exception as an error.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    void Error(Exception exception);

    /// <summary>
    /// Logs an exception as an error with optional event identifier.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Error(Exception exception, EventId? eventId = null);

    /// <summary>
    /// Logs an error with custom message and exception details.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="exception">The associated exception.</param>
    void Error(string message, Exception exception);

    /// <summary>
    /// Logs an error with message, exception, and optional event identifier.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Error(string message, Exception exception, EventId? eventId = null);

    /// <summary>
    /// Logs a critical error that may cause application failure.
    /// </summary>
    /// <param name="message">The critical error message to log.</param>
    void Fatal(string message);

    /// <summary>
    /// Logs a critical error with optional event identifier.
    /// </summary>
    /// <param name="message">The critical error message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Fatal(string message, EventId? eventId = null);

    /// <summary>
    /// Logs a critical error with custom message and exception details.
    /// </summary>
    /// <param name="message">The critical error message to log.</param>
    /// <param name="exception">The associated exception.</param>
    void Fatal(string message, Exception exception);

    /// <summary>
    /// Logs a critical error with message, exception, and optional event identifier.
    /// </summary>
    /// <param name="message">The critical error message to log.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Fatal(string message, Exception exception, EventId? eventId = null);
}
