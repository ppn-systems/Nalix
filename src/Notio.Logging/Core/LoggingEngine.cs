using Notio.Common.Enums;
using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Logging.Targets.File;
using System;

namespace Notio.Logging.Core;

/// <summary>
/// Abstract class that provides a logging engine functionality to handle log entries.
/// </summary>
public abstract class LoggingEngine
{
    private readonly LoggingPublisher _publisher = new();
    internal FileLoggerOptions Options { get; init; } = new();

    /// <summary>
    /// Gets the logging publisher instance.
    /// </summary>
    public ILoggingPublisher Publisher => _publisher;

    /// <summary>
    /// The minimum logging level required to log messages.
    /// </summary>
    public LoggingLevel MinimumLevel = LoggingLevel.Trace;

    /// <summary>
    /// Checks if the log level is greater than or equal to the minimum required level.
    /// </summary>
    /// <param name="level">The log level to check.</param>
    /// <returns><c>true</c> if the log level is above or equal to the minimum level, otherwise <c>false</c>.</returns>
    protected bool CanLog(LoggingLevel level) => level >= MinimumLevel;

    /// <summary>
    /// Creates a log entry and publishes it if the log level is valid.
    /// </summary>
    /// <param name="level">The level of the log entry (e.g., Trace, Info, Error).</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="error">Optional exception information to include with the log entry.</param>
    protected void CreateLogEntry(LoggingLevel level, EventId eventId, string message, Exception? error = null)
    {
        if (!CanLog(level)) return;

        _publisher.Publish(new LoggingEntry(level, eventId, message, error));
    }
}