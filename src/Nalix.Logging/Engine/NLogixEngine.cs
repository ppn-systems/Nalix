// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration;
using Nalix.Logging.Configuration;
using Nalix.Logging.Sinks;

namespace Nalix.Logging.Engine;

/// <summary>
/// Abstract class that provides a high-performance logging engine to process log entries.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerDisplay("{GetType().Name,nq}")]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class NLogixEngine : IDisposable
{
    #region Fields

    private readonly NLogixOptions _logOptions;
    private readonly NLogixDistributor _distributor;

    private LogLevel _minLevel;
    private int _isDisposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="NLogixEngine"/> class.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    protected NLogixEngine(Action<NLogixOptions>? configureOptions = null)
    {
        _distributor = new NLogixDistributor();
        _logOptions = ConfigurationManager.Instance.Get<NLogixOptions>();

        _ = _logOptions.SetPublisher(_distributor);

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
                _ = cfg.RegisterTarget(new BatchFileLogTarget());
                _ = cfg.RegisterTarget(new BatchConsoleLogTarget());
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected void ConfigureOptions(Action<NLogixOptions> configureOptions)
    {
        configureOptions?.Invoke(_logOptions);

        LogLevel newLevel = _logOptions.MinLevel;
        _ = Interlocked.Exchange(ref Unsafe
                       .As<LogLevel, int>(ref _minLevel), (int)newLevel);
    }

    /// <summary>
    /// Checks if the log level meets the minimum required level for logging.
    /// </summary>
    /// <param name="level">The log level to check.</param>
    /// <returns><c>true</c> if the log level is enabled for logging.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsEnabled(LogLevel level) => level >= _minLevel;

    /// <inheritdoc/>
    public virtual IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <inheritdoc/>
    public virtual void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ArgumentNullException.ThrowIfNull(formatter);

        if (_isDisposed != 0 || !this.IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null)
        {
            return;
        }

        this.Publish(logLevel, eventId, message, exception);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        private NullScope() { }
        public void Dispose() { }
    }

    /// <summary>
    /// Creates and publishes a log entry if the log level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="eventId">The event identifier associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="error">Optional exception information.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage(
        "Reliability", "CA2012:Use ValueTasks correctly", Justification = "<Pending>")]
    [SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    protected void Publish(LogLevel level, EventId? eventId, string message, Exception? error = null)
    {
        if (_isDisposed != 0)
        {
            return;
        }

        LogEntry entry = new(level, eventId, message, error);
        _distributor.Publish(entry);
    }

    /// <summary>
    /// Releases managed and unmanaged resources used by the logging engine.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    public virtual void Dispose(bool disposing)
    {
        // Thread-safe disposal check using Interlocked
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
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
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion Disposable
}
