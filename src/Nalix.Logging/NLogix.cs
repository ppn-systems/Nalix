using Nalix.Common.Logging;
using Nalix.Logging.Engine;
using Nalix.Logging.Options;
using System;
using System.Runtime.CompilerServices;

namespace Nalix.Logging;

/// <summary>
/// Provides a high-performance, extensible logging engine for applications,
/// combining structured logging and customizable output targets.
/// 
/// This class is the core of the Nalix logging system, and implements <see cref="ILogger"/> for unified logging.
/// Use this logger to write diagnostic messages, errors, warnings, or audit logs across the application.
/// </summary>
/// <remarks>
/// The <see cref="NLogix"/> logger supports dependency injection or can be accessed via <see cref="NLogixSingleton"/>.
/// Logging targets and behavior can be customized during initialization using <see cref="LogOptions"/>.
/// </remarks>
public class NLogix : LogEngine, ILogger
{
    #region Constructors

    /// <summary>
    /// Initializes the logging system with optional configuration.
    /// </summary>
    /// <param name="configure">An optional action to configure the logging system.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public NLogix(Action<LogOptions>? configure = null)
        : base(configure)
    {
    }

    #endregion

    #region Meta Methods

    /// <inheritdoc />
    public void Meta(string message)
        => WriteLog(LogLevel.Meta, EventId.Empty, message);

    /// <inheritdoc />
    public void Meta(string format, params object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Meta, EventId.Empty, format, args);

    /// <inheritdoc />
    public void Meta(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Meta, eventId ?? EventId.Empty, message);

    #endregion

    #region Trace Methods

    /// <inheritdoc />
    public void Trace(string message)
        => WriteLog(LogLevel.Trace, EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    public void Trace(string format, params object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Trace, EventId.Empty, format, args);

    /// <inheritdoc />
    public void Trace(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Trace, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    #endregion

    #region Debug Methods

    /// <inheritdoc />
    public void Debug(string message)
        => WriteLog(LogLevel.Debug, EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    public void Debug(string format, params object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Debug, EventId.Empty, format, args);

    /// <inheritdoc />
    public void Debug(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Debug, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    public void Debug<TClass>(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
        where TClass : class
        => WriteLog(LogLevel.Debug, eventId ?? EventId.Empty, $"[{typeof(TClass).Name}:{memberName}] {message}");

    #endregion

    #region Info Methods

    /// <inheritdoc />
    public void Info(string message)
        => WriteLog(LogLevel.Information, EventId.Empty, message);

    /// <inheritdoc />
    public void Info(string format, params object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Information, EventId.Empty, format, args);

    /// <inheritdoc />
    public void Info(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Information, eventId ?? EventId.Empty, message);

    #endregion

    #region Warn Methods

    /// <inheritdoc />
    public void Warn(string message)
        => WriteLog(LogLevel.Warning, EventId.Empty, message);

    /// <inheritdoc />
    public void Warn(string format, params object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Warning, EventId.Empty, format, args);

    /// <inheritdoc />
    public void Warn(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Warning, eventId ?? EventId.Empty, message);

    #endregion

    #region Error Methods

    /// <inheritdoc />
    public void Error(string message)
        => WriteLog(LogLevel.Error, EventId.Empty, message);

    /// <inheritdoc />
    public void Error(string format, params object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Error, EventId.Empty, format, args);

    /// <inheritdoc />
    public void Error(Exception exception)
        => WriteLog(LogLevel.Error, EventId.Empty, exception.Message, exception);

    /// <inheritdoc />
    public void Error(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Error, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Error(Exception exception, EventId? eventId = null)
        => WriteLog(LogLevel.Error, eventId ?? EventId.Empty, exception.Message, exception);

    /// <inheritdoc />
    public void Error(string message, Exception exception)
        => WriteLog(LogLevel.Error, EventId.Empty, message, exception);

    /// <inheritdoc />
    public void Error(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LogLevel.Error, eventId ?? EventId.Empty, message, exception);

    #endregion

    #region Fatal Methods

    /// <inheritdoc />
    public void Fatal(string message)
        => WriteLog(LogLevel.Critical, EventId.Empty, message);

    /// <inheritdoc />
    public void Fatal(string format, params object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Critical, EventId.Empty, format, args);

    /// <inheritdoc />
    public void Fatal(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Critical, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Fatal(string message, Exception exception)
        => WriteLog(LogLevel.Critical, EventId.Empty, message, exception);

    /// <inheritdoc />
    public void Fatal(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LogLevel.Critical, eventId ?? EventId.Empty, message, exception);

    #endregion

    #region Private Methods

    // Sanitize log message to prevent log forging
    // Removes potentially dangerous characters (e.g., newlines or control characters)
    private static string SanitizeLogMessage(string? message)
        => message?.Replace("\n", "").Replace("\r", "") ?? string.Empty;

    /// <summary>
    /// Writes a log entry with the specified level, event Number, message, and optional exception.
    /// </summary>
    /// <param name="level">The log level (e.g., Info, Warning, Error, etc.).</param>
    /// <param name="eventId">The event Number to associate with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception associated with the log entry.</param>
    private void WriteLog(LogLevel level, EventId eventId, string message, System.Exception? exception = null)
       => base.CreateLogEntry(level, eventId, message, exception);

    #endregion
}
