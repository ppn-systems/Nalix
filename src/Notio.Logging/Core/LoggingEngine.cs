using Notio.Common.Logging;
using Notio.Logging.Options;
using Notio.Logging.Targets;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Logging.Core;

/// <summary>
/// Abstract class that provides a high-performance logging engine to process log entries.
/// </summary>
public abstract class LoggingEngine : IDisposable
{
    #region Fields

    private readonly LogLevel _minLevel;
    private readonly LoggingPublisher _publisher;
    private readonly LoggingOptions _loggingOptions;

    private int _isDisposed;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingEngine"/> class.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    protected LoggingEngine(Action<LoggingOptions>? configureOptions = null)
    {
        _publisher = new LoggingPublisher();
        _loggingOptions = new LoggingOptions(_publisher);

        // Apply configuration if provided
        if (configureOptions != null)
        {
            configureOptions.Invoke(_loggingOptions);
        }
        else
        {
            // Apply default configuration
            _loggingOptions.ConfigureDefaults(cfg =>
            {
                cfg.AddTarget(new ConsoleLoggingTarget());
                cfg.AddTarget(new FileLoggingTarget(_loggingOptions.FileOptions));
                return cfg;
            });
        }

        // Cache min level for faster checks
        _minLevel = _loggingOptions.MinLevel;
    }

    /// <summary>
    /// Reconfigure logging options after initialization.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    public void Configure(Action<LoggingOptions> configureOptions)
        => configureOptions?.Invoke(_loggingOptions);

    /// <summary>
    /// Checks if the log level meets the minimum required level for logging.
    /// </summary>
    /// <param name="level">The log level to check.</param>
    /// <returns><c>true</c> if the log level is enabled for logging.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool IsEnabled(LogLevel level) => level >= _minLevel;

    /// <summary>
    /// Creates and publishes a log entry if the log level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="error">Optional exception information.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void CreateLogEntry(LogLevel level, EventId eventId, string message, Exception? error = null)
    {
        if (_isDisposed != 0 || level < _minLevel)
            return;

        // Create and publish the log entry
        _publisher.Publish(new LogEntry(level, eventId, message, error));
    }

    /// <summary>
    /// Creates and publishes a log entry with a formatted message if the log level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="format">The message format string with placeholders.</param>
    /// <param name="args">The argument values for the format string.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void CreateFormattedLogEntry(LogLevel level, EventId eventId, string format, params object[] args)
    {
        // Skip expensive string formatting if the log level is disabled
        if (_isDisposed != 0 || level < _minLevel)
            return;

        // Format the message only if we're going to use it
        CreateLogEntry(level, eventId, FormatMessage(format, args));
    }

    /// <summary>
    /// Gets the current timestamp for logging operations.
    /// </summary>
    /// <returns>A UTC timestamp for logging.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static DateTime GetTimestamp() => DateTime.UtcNow;

    /// <summary>
    /// Disposes the logging engine and all related resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed and unmanaged resources used by the logging engine.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        // Thread-safe disposal check using Interlocked
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        if (disposing)
        {
            _loggingOptions.Dispose();
        }
    }

    private static string FormatMessage(string format, object[]? args)
        => args == null || args.Length == 0 ? format : string.Format(format, args);
}
