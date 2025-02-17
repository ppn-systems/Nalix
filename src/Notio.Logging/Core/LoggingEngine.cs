using Notio.Common.Enums;
using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Logging.Targets;
using System;

namespace Notio.Logging.Core;

/// <summary>
/// Abstract class that provides a logging engine functionality to handle log entries.
/// </summary>
public abstract class LoggingEngine
{
    private readonly LoggingPublisher _publisher = new();

    /// <summary>
    /// Gets the logging publisher instance.
    /// </summary>
    private ILoggingPublisher Publisher => _publisher;

    /// <summary>
    /// The logging configuration options for the application.
    /// This property holds the configuration settings for the logging system,
    /// such as the logging level, file options, and any other logging-related settings.
    /// </summary>
    public LoggingOptions LoggingOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingEngine"/> class.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the <see cref="LoggingOptions"/> instance.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    public LoggingEngine(Action<LoggingOptions>? configureOptions = null)
    {
        LoggingOptions = new LoggingOptions(Publisher);
        configureOptions?.Invoke(LoggingOptions);

        if (configureOptions is null)
        {
            LoggingOptions.ConfigureDefaults(cfg =>
            {
                cfg.AddTarget(new ConsoleLoggingTarget());
                cfg.AddTarget(new FileLoggingTarget(LoggingOptions.FileOptions));
                return cfg;
            });
        }
    }

    /// <summary>
    /// Checks if the log level is greater than or equal to the minimum required level.
    /// </summary>
    /// <param name="level">The log level to check.</param>
    /// <returns><c>true</c> if the log level is above or equal to the minimum level, otherwise <c>false</c>.</returns>
    protected bool CanLog(LoggingLevel level) => level >= LoggingOptions.MinLevel;

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
