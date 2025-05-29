namespace Nalix.Common.Logging;

/// <summary>
/// Defines the contract for a logging system that provides hierarchical logging levels.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Logs metadata information useful for system configuration and setup.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Meta(System.String message);

    /// <summary>
    /// Logs metadata information with format and arguments.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The arguments to format.</param>
    void Meta(System.String format, params System.Object[] args);

    /// <summary>
    /// Logs metadata information with optional event identifier.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Meta(System.String message, EventId? eventId = null);

    /// <summary>
    /// Logs trace-level information for detailed diagnostics.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Trace(System.String message);

    /// <summary>
    /// Logs trace information with format and arguments.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The arguments to format.</param>
    void Trace(System.String format, params System.Object[] args);

    /// <summary>
    /// Logs trace-level information with optional event identifier.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Trace(System.String message, EventId? eventId = null);

    /// <summary>
    /// Logs debug information for development and troubleshooting.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Debug(System.String message);

    /// <summary>
    /// Logs debug information with format and arguments.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The arguments to format.</param>
    void Debug(System.String format, params System.Object[] args);

    /// <summary>
    /// Logs debug information with optional event identifier.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Debug(System.String message, EventId? eventId = null);

    /// <summary>
    /// Logs debug information for a specific class type.
    /// </summary>
    /// <typeparam name="TClass">The class type for context.</typeparam>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    /// <param name="memberName">Optional member name where logging occurs.</param>
    void Debug<TClass>(System.String message, EventId? eventId = null, System.String memberName = "")
        where TClass : class;

    /// <summary>
    /// Logs information about normal application flow.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Info(System.String message);

    /// <summary>
    /// Logs information with format and arguments.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The arguments to format.</param>
    void Info(System.String format, params System.Object[] args);

    /// <summary>
    /// Logs information with optional event identifier.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Info(System.String message, EventId? eventId = null);

    /// <summary>
    /// Logs a warning about potential issues.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    void Warn(System.String message);

    /// <summary>
    /// Logs warning information with format and arguments.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The arguments to format.</param>
    void Warn(System.String format, params System.Object[] args);

    /// <summary>
    /// Logs a warning with optional event identifier.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Warn(System.String message, EventId? eventId = null);

    /// <summary>
    /// Logs an error message about handled exceptions.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    void Error(System.String message);

    /// <summary>
    /// Logs an error message with optional event identifier.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Error(System.String message, EventId? eventId = null);

    /// <summary>
    /// Logs an exception as an error.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    void Error(System.Exception exception);

    /// <summary>
    /// Logs an error message with format and arguments.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The arguments to format.</param>
    void Error(System.String format, params System.Object[] args);

    /// <summary>
    /// Logs an exception as an error with optional event identifier.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Error(System.Exception exception, EventId? eventId = null);

    /// <summary>
    /// Logs an error with custom message and exception details.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="exception">The associated exception.</param>
    void Error(System.String message, System.Exception exception);

    /// <summary>
    /// Logs an error with message, exception, and optional event identifier.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Error(System.String message, System.Exception exception, EventId? eventId = null);

    /// <summary>
    /// Logs a critical error that may cause application failure.
    /// </summary>
    /// <param name="message">The critical error message to log.</param>
    void Fatal(System.String message);

    /// <summary>
    /// Logs a critical error with format and arguments.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The arguments to format.</param>
    void Fatal(System.String format, params System.Object[] args);

    /// <summary>
    /// Logs a critical error with custom message and exception details.
    /// </summary>
    /// <param name="message">The critical error message to log.</param>
    /// <param name="exception">The associated exception.</param>
    void Fatal(System.String message, System.Exception exception);

    /// <summary>
    /// Logs a critical error with optional event identifier.
    /// </summary>
    /// <param name="message">The critical error message to log.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Fatal(System.String message, EventId? eventId = null);

    /// <summary>
    /// Logs a critical error with message, exception, and optional event identifier.
    /// </summary>
    /// <param name="message">The critical error message to log.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="eventId">Optional event identifier for correlation.</param>
    void Fatal(System.String message, System.Exception exception, EventId? eventId = null);
}
