// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Nalix.Logging;

/// <summary>
/// High-performance publisher that dispatches log entries to registered logging targets.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NLogixDistributor : INLogixDistributor
{
    #region Fields

    /// <summary>
    /// Use a dummy value (0) for dictionary entries as we only care about the keys
    /// </summary>
    private const byte DummyValue = 0;

    /// <summary>
    /// Using a concurrent dictionary for thread-safe operations on targets
    /// </summary>
    private INLogixTarget[]? _targetsCache;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<INLogixTarget, byte> _targets = new();

    /// <summary>
    /// Track disposed state in a thread-safe way
    /// </summary>
    private int _isDisposed;

    private long _totalEntriesPublished;
    private long _totalTargetInvocations;
    private long _totalPublishErrors;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total count of errors that occurred during publish operations.
    /// </summary>
    public long TotalPublishErrors
        => Interlocked.Read(ref _totalPublishErrors);

    /// <summary>
    /// Gets the total number of log entries that have been published.
    /// </summary>
    public long TotalEntriesPublished
        => Interlocked.Read(ref _totalEntriesPublished);

    /// <summary>
    /// Gets the total number of target publish operations performed.
    /// </summary>
    public long TotalTargetInvocations
        => Interlocked.Read(ref _totalTargetInvocations);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Publishes a log entry to all registered logging targets.
    /// </summary>
    /// <param name="timestampUtc">The UTC timestamp assigned to the log event.</param>
    /// <param name="logLevel">The severity level of the log event.</param>
    /// <param name="eventId">The associated event identifier.</param>
    /// <param name="message">The rendered log message.</param>
    /// <param name="exception">The associated exception, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Publish(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Quick check for disposed state
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        INLogixTarget[] targets = _targetsCache ??= [.. _targets.Keys];

        if (targets.Length == 0)
        {
            return;
        }

        // Increment the published entries counter
        _ = Interlocked.Increment(ref _totalEntriesPublished);

        for (int i = 0; i < targets.Length; i++)
        {
            try
            {
                targets[i].Publish(timestampUtc, logLevel, eventId, message, exception);
                _ = Interlocked.Increment(ref _totalTargetInvocations);
            }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                _ = Interlocked.Increment(ref _totalPublishErrors);
                HandleTargetError(targets[i], ex, timestampUtc, logLevel, eventId, message, exception);
            }
        }
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    /// <param name="loggerHandler">The logging target to add.</param>
    /// <returns>The current instance of <see cref="INLogixDistributor"/>, allowing method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public INLogixDistributor RegisterTarget(INLogixTarget loggerHandler)
    {
        ArgumentNullException.ThrowIfNull(loggerHandler);
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        _ = _targets.TryAdd(loggerHandler, DummyValue);
        _targetsCache = null;  // invalidate
        return this;
    }

    /// <summary>
    /// Removes a logging target from the publisher.
    /// </summary>
    /// <param name="loggerHandler">The logging target to remove.</param>
    /// <returns><c>true</c> if the target was successfully removed; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool UnregisterTarget(INLogixTarget loggerHandler)
    {
        ArgumentNullException.ThrowIfNull(loggerHandler);
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        bool removed = _targets.TryRemove(loggerHandler, out _);
        if (removed)
        {
            _targetsCache = null;
        }

        return removed;
    }

    /// <summary>
    /// Handles errors that occur when publishing to a target.
    /// </summary>
    /// <param name="target">The target that caused the error.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="timestampUtc">The UTC timestamp assigned to the original log event.</param>
    /// <param name="logLevel">The severity level of the original log event.</param>
    /// <param name="eventId">The event identifier of the original log event.</param>
    /// <param name="message">The rendered message of the original log event.</param>
    /// <param name="originalException">The exception attached to the original log event, if any.</param>
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
            // Log to debug output at minimum
#if DEBUG
            Debug.WriteLine(
                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR publishing to " +
                $"{target.GetType().Name}: {exception.Message}");
#endif

            // Check if target implements error handling
            if (target is INLogixErrorHandler errorHandler)
            {
                errorHandler.HandleError(exception, timestampUtc, logLevel, eventId, message, originalException);
            }
        }
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            // Ignore errors in the error handler to prevent cascading failures
        }
    }

    /// <summary>
    /// Disposes of the logging publisher and its targets if applicable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Dispose()
    {
        // Thread-safe disposal check
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            // Dispose each target if it implements IDisposable
            foreach (IDisposable target in
                Enumerable.OfType<IDisposable>(_targets.Keys))
            {
                try
                {
                    target.Dispose();
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    // Log disposal errors to debug output
#if DEBUG
                    Debug.WriteLine(
                        $"ERROR disposing logging target: {ex.Message}");
#else
                    GC.KeepAlive(ex);
#endif
                }
            }

            _targets.Clear();
        }
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            // Log final disposal errors to debug output
#if DEBUG
            Debug.WriteLine(
                $"ERROR during NLogixDistributor disposal: {ex.Message}");
#else
            GC.KeepAlive(ex);
#endif
        }
    }

    /// <summary>
    /// Creates a diagnostic report about the publisher's state.
    /// </summary>
    /// <returns>A string containing diagnostic information.</returns>
    public override string ToString()
        => $"[NLogixDistributor Stats - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]" + System.Environment.NewLine +
           $"- USER: {System.Environment.UserName}" + System.Environment.NewLine +
           $"- Active Targets: {_targets.Count}" + System.Environment.NewLine +
           $"- Entries Published: {this.TotalEntriesPublished:N0}" + System.Environment.NewLine +
           $"- Target Operations: {this.TotalTargetInvocations:N0}" + System.Environment.NewLine +
           $"- Errors: {this.TotalPublishErrors:N0}" + System.Environment.NewLine +
           $"- Disposed: {_isDisposed != 0}" + System.Environment.NewLine;

    #endregion Public Methods
}
