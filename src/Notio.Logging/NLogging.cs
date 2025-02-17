using Notio.Common.Enums;
using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Logging.Core;
using Notio.Logging.Targets;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Logging;

/// <summary>
/// A singleton class that provides logging functionality for the application.
/// </summary>
/// <remarks>
/// Initializes the logging system with optional configuration.
/// </remarks>
/// <param name="configure">An optional action to configure the logging system.</param>
public sealed class NLogging(Action<LoggingOptions>? configure = null) : LoggingEngine(configure), ILogger
{
    /// <summary>
    /// Gets the single instance of the <see cref="NLogging"/> class.
    /// </summary>
    public static NLogging Instance { get; } = new(cfg => cfg
        .AddTarget(new ConsoleLoggingTarget())
        .AddTarget(new FileLoggingTarget())
    );

    /// <summary>
    /// Writes a log entry with the specified level, event ID, message, and optional exception.
    /// </summary>
    /// <param name="level">The log level (e.g., Info, Warning, Error, etc.).</param>
    /// <param name="eventId">The event ID to associate with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception associated with the log entry.</param>
    public void WriteLog(LoggingLevel level, EventId eventId, string message, Exception? exception = null)
       => base.CreateLogEntry(level, eventId, message, exception);

    /// <inheritdoc />
    public void Meta(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Meta, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Trace(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Trace, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    public void Debug(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
        => WriteLog(LoggingLevel.Debug, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    public void Debug<TClass>(string message, EventId? eventId = null, [CallerMemberName] string memberName = "")
        where TClass : class
        => WriteLog(LoggingLevel.Debug, eventId ?? EventId.Empty, $"[{typeof(TClass).Name}:{memberName}] {message}");

    /// <inheritdoc />
    public void Info(string format, params object[] args)
        => WriteLog(LoggingLevel.Information, EventId.Empty, string.Format(format, args));

    /// <inheritdoc />
    public void Info(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Information, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Warn(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Warning, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Error(Exception exception, EventId? eventId = null)
        => WriteLog(LoggingLevel.Error, eventId ?? EventId.Empty, exception.Message, exception);

    /// <inheritdoc />
    public void Error(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LoggingLevel.Error, eventId ?? EventId.Empty, message, exception);

    /// <inheritdoc />
    public void Error(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Error, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Fatal(string message, EventId? eventId = null)
        => WriteLog(LoggingLevel.Critical, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    public void Fatal(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LoggingLevel.Critical, eventId ?? EventId.Empty, message, exception);

    // Sanitize log message to prevent log forging
    // Removes potentially dangerous characters (e.g., newlines or control characters)
    private static string SanitizeLogMessage(string message)
        => message?.Replace("\n", "").Replace("\r", "") ?? string.Empty;
}
