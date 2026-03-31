// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Framework.Configuration;
using Nalix.Logging.Configuration;
using Nalix.Logging.Sinks;

namespace Nalix.Logging;

/// <summary>
/// <para>
/// Provides a high-performance, extensible logging engine for applications,
/// combining structured logging and customizable output targets.
/// </para>
/// <para>
/// This class is the core of the Nalix logging system, and implements <see cref="ILogger"/> for unified logging.
/// Use this logger to write diagnostic messages, errors, warnings, or audit logs across the application.
/// </para>
/// </summary>
/// <remarks>
/// The <see cref="NLogix"/> logger supports dependency injection or can be accessed via <see cref="Host"/>.
/// Logging targets and behavior can be customized during initialization using <see cref="NLogixOptions"/>.
/// </remarks>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("Logger=NLogix, {GetType().Name,nq}")]
public sealed partial class NLogix : ILogger, IDisposable
{
    #region Fields

    private readonly NLogixOptions _logOptions;
    private readonly NLogixDistributor _distributor;

    private LogLevel _minLevel;
    private int _isDisposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="NLogix"/> class.
    /// </summary>
    /// <param name="configureOptions">
    /// An action that allows configuring the logging options.
    /// This action is used to set up logging options such as the minimum logging level and file options.
    /// </param>
    public NLogix(Action<NLogixOptions>? configureOptions = null)
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
    public void ConfigureOptions(Action<NLogixOptions> configureOptions)
    {
        configureOptions?.Invoke(_logOptions);

        LogLevel newLevel = _logOptions.MinLevel;
        _ = Interlocked.Exchange(ref Unsafe.As<LogLevel, int>(ref _minLevel), (int)newLevel);
    }

    /// <summary>
    /// Checks if the log level meets the minimum required level for logging.
    /// </summary>
    /// <param name="logLevel">The log level to check.</param>
    /// <returns><c>true</c> if the log level is enabled for logging.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ArgumentNullException.ThrowIfNull(formatter);

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
    public void Publish(LogLevel level, EventId? eventId, string message, Exception? error = null)
    {
        if (_isDisposed != 0)
        {
            return;
        }

        _distributor.Publish(DateTime.UtcNow, level, eventId ?? default, message, error);
    }

    /// <summary>
    /// Releases managed and unmanaged resources used by the logging engine.
    /// </summary>
    public void Dispose()
    {
        // Thread-safe disposal check using Interlocked
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _logOptions.Dispose();
    }

    #endregion Logging Methods
}
