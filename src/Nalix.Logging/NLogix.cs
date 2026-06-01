// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Logging.Options;

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
/// Use the <see cref="Extensions.NLogixFx.Configure"/> method or <see cref="INLogixBuilder"/> to create instances.
/// </remarks>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("Logger=NLogix, Targets={_targets.Length}, Min={_minLevel}")]
public sealed partial class NLogix : ILogger, IDisposable
{
    #region Fields

    private readonly INLogixTarget[] _targets;

    private readonly LogLevel _minLevel;
    private int _isDisposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="NLogix"/> class.
    /// </summary>
    /// <param name="targets">The logging targets that will receive log entries.</param>
    /// <param name="options">The logging configuration options.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="targets"/> or <paramref name="options"/> is null.
    /// </exception>
    public NLogix(INLogixTarget[] targets, NLogixOptions options)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(options);

        _targets = targets;
        _minLevel = options.MinLevel;
    }

    #endregion Constructors

    #region Logging Methods

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

        DateTime timestamp = DateTime.UtcNow;
        EventId id = eventId ?? default;

        for (int i = 0; i < _targets.Length; i++)
        {
            try
            {
                _targets[i].Publish(timestamp, level, id, message, error);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                HandleTargetError(_targets[i], ex, timestamp, level, id, message, error);
            }
        }
    }

    /// <summary>
    /// Handles an error that occurred when publishing to a specific target.
    /// If the target implements <see cref="INLogixErrorHandler"/>, the error is delegated to it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void HandleTargetError(
        INLogixTarget target,
        Exception exception,
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? originalException)
    {
        try
        {
#if DEBUG
            Debug.WriteLine(
                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR publishing to {target.GetType().Name}: {exception.Message}");
#endif

            if (target is INLogixErrorHandler errorHandler)
            {
                errorHandler.HandleError(exception, timestampUtc, logLevel, eventId, message, originalException);
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            // Ignore errors in the error handler to prevent cascading failures
        }
    }

    /// <summary>
    /// Releases managed and unmanaged resources used by the logging engine.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        for (int i = 0; i < _targets.Length; i++)
        {
            if (_targets[i] is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
#if DEBUG
                    Debug.WriteLine($"ERROR disposing logging target: {ex.Message}");
#else
                    GC.KeepAlive(ex);
#endif
                }
            }
        }

        GC.SuppressFinalize(this);
    }

    #endregion Logging Methods
}
