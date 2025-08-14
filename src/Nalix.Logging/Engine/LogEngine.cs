// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Sinks.Console;
using Nalix.Logging.Sinks.File;

namespace Nalix.Logging.Engine;

/// <summary>
/// Abstract class that provides a high-performance logging engine to process log entries.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("{GetType().Name,nq}")]
public abstract class LogEngine : System.IDisposable
{
    #region Fields

    private readonly LogLevel _minLevel;
    private readonly NLogOptions _logOptions;
    private readonly LogDistributor _distributor;

    private System.Int32 _isDisposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEngine"/> class.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    protected LogEngine(System.Action<NLogOptions>? configureOptions = null)
    {
        _distributor = new LogDistributor();
        _logOptions = new NLogOptions(_distributor);

        // Apply configuration if provided
        if (configureOptions != null)
        {
            configureOptions.Invoke(_logOptions);
        }
        else
        {
            // Apply default configuration
            _ = _logOptions.ConfigureDefaults(cfg =>
            {
                _ = cfg.AddTarget(new ConsoleLogTarget());
                _ = cfg.AddTarget(new FileLogTarget(_logOptions.FileOptions));
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected void Configure(System.Action<NLogOptions> configureOptions)
        => configureOptions?.Invoke(_logOptions);

    /// <summary>
    /// Checks if the log level meets the minimum required level for logging.
    /// </summary>
    /// <param name="level">The log level to check.</param>
    /// <returns><c>true</c> if the log level is enabled for logging.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected System.Boolean IsEnabled(LogLevel level) => level >= _minLevel;

    /// <summary>
    /// Creates and publishes a log entry if the log level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="error">Optional exception information.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected void CreateLogEntry(
        LogLevel level, EventId eventId,
        System.String message, System.Exception? error = null)
    {
        if (_isDisposed != 0 || level < _minLevel)
        {
            return;
        }

        // Create and publish the log entry
        _ = _distributor.PublishAsync(new LogEntry(level, eventId, message, error));
    }

    /// <summary>
    /// Creates and publishes a log entry with a formatted message if the log level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="format">The message format string with placeholders.</param>
    /// <param name="args">The argument values for the format string.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    protected void CreateFormattedLogEntry(
        LogLevel level, EventId eventId,
        System.String format, params System.Object[] args)
    {
        // Skip expensive string formatting if the log level is disabled
        if (_isDisposed != 0 || level < _minLevel)
        {
            return;
        }

        // Format the message only if we're going to use it
        CreateLogEntry(level, eventId, FormatMessage(format, args));
    }

    /// <summary>
    /// Releases managed and unmanaged resources used by the logging engine.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(System.Boolean disposing)
    {
        // Thread-safe disposal check using Interlocked
        if (System.Threading.Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

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
        System.GC.SuppressFinalize(this);
    }

    #endregion Disposable

    #region Private Methods

    [System.Diagnostics.Contracts.Pure]
    private static System.String FormatMessage(System.String format, System.Object[]? args)
        => args == null || args.Length == 0 ? format : System.String.Format(format, args);

    #endregion Private Methods
}