using Nalix.Common.Logging;
using Nalix.Logging.Options;
using Nalix.Logging.Targets;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Logging.Engine;

/// <summary>
/// Abstract class that provides a high-performance logging engine to process log entries.
/// </summary>
public abstract class LogEngine : IDisposable
{
    #region Fields

    private readonly LogLevel _minLevel;
    private readonly LogOptions _logOptions;
    private readonly LogDistributor _distributor;

    private int _isDisposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEngine"/> class.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    protected LogEngine(Action<LogOptions>? configureOptions = null)
    {
        _distributor = new LogDistributor();
        _logOptions = new LogOptions(_distributor);

        // Apply configuration if provided
        if (configureOptions != null)
        {
            configureOptions.Invoke(_logOptions);
        }
        else
        {
            // Apply default configuration
            _logOptions.ConfigureDefaults(cfg =>
            {
                cfg.AddTarget(new ConsoleLogTarget());
                cfg.AddTarget(new FileLogTarget(_logOptions.FileOptions));
                return cfg;
            });
        }

        // Cache min level for faster checks
        _minLevel = _logOptions.MinLevel;
    }

    #endregion Constructors

    #region Logging Methods

    /// <summary>
    /// Reconfigure logging options after initialization.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    protected void Configure(Action<LogOptions> configureOptions)
        => configureOptions?.Invoke(_logOptions);

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
        _distributor.Publish(new LogEntry(level, eventId, message, error));
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
            _logOptions.Dispose();
        }
    }

    #endregion Logging Methods

    #region Disposable

    /// <summary>
    /// Disposes the logging engine and all related resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion Disposable

    #region Private Methods

    private static string FormatMessage(string format, object[]? args)
        => args == null || args.Length == 0 ? format : string.Format(format, args);

    #endregion Private Methods
}
