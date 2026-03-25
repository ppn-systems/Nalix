// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;

namespace Nalix.Logging.Engine;

/// <summary>
/// High-performance publisher that dispatches log entries to registered logging targets.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NLogixDistributor : ILogDistributor
{
    #region Fields

    /// <summary>
    /// Use a dummy value (0) for dictionary entries as we only care about the keys
    /// </summary>
    private const byte DummyValue = 0;

    /// <summary>
    /// Using a concurrent dictionary for thread-safe operations on targets
    /// </summary>
    private ILoggerTarget[]? _targetsCache;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ILoggerTarget, byte> _targets = new();

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
    /// Gets the ProtocolType of errors that occurred during publish operations.
    /// </summary>
    public long TotalPublishErrors
        => Interlocked.Read(ref _totalPublishErrors);

    /// <summary>
    /// Gets the total ProtocolType of log entries that have been published.
    /// </summary>
    public long TotalEntriesPublished
        => Interlocked.Read(ref _totalEntriesPublished);

    /// <summary>
    /// Gets the total ProtocolType of target publish operations performed.
    /// </summary>
    public long TotalTargetInvocations
        => Interlocked.Read(ref _totalTargetInvocations);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Publishes a log entry to all registered logging targets.
    /// </summary>
    /// <param name="entry">The log entry to be published.</param>
    /// <exception cref="ArgumentNullException">Thrown if entry is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public void Publish(LogEntry? entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        // Quick check for disposed state
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        ILoggerTarget[] targets = _targetsCache ??= [.. _targets.Keys];

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
                targets[i].Publish(entry.Value);
                _ = Interlocked.Increment(ref _totalTargetInvocations);
            }
            catch (Exception ex)
            {
                _ = Interlocked.Increment(ref _totalPublishErrors);
                HandleTargetError(targets[i], ex, entry.Value);
            }
        }
    }

    /// <summary>
    /// Publishes a log entry asynchronously to all registered logging targets.
    /// </summary>
    /// <param name="entry">The log entry to be published.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if entry is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    public ValueTask PublishAsync(LogEntry? entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        // Quick check for disposed state
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        ILoggerTarget[] targets = _targetsCache ??= [.. _targets.Keys];

        if (targets.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            try
            {
                targets[i].Publish(entry.Value);
                _ = Interlocked.Increment(ref _totalTargetInvocations);
            }
            catch (Exception ex)
            {
                _ = Interlocked.Increment(ref _totalPublishErrors);
                HandleTargetError(targets[i], ex, entry.Value);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    /// <param name="loggerHandler">The logging target to add.</param>
    /// <returns>The current instance of <see cref="ILogDistributor"/>, allowing method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public ILogDistributor RegisterTarget(ILoggerTarget loggerHandler)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public bool UnregisterTarget(ILoggerTarget loggerHandler)
    {
        ArgumentNullException.ThrowIfNull(loggerHandler);
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        return _targets.TryRemove(loggerHandler, out _);
    }

    /// <summary>
    /// Handles errors that occur when publishing to a target.
    /// </summary>
    /// <param name="target">The target that caused the error.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="entry">The log entry being published.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static void HandleTargetError(ILoggerTarget target, Exception exception, LogEntry entry)
    {
        try
        {
            // Log to debug output at minimum
            Debug.WriteLine(
                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR publishing to " +
                $"{target.GetType().Name}: {exception.Message}");

            // Check if target implements error handling
            if (target is ILoggerErrorHandler errorHandler)
            {
                errorHandler.HandleError(exception, entry);
            }
        }
        catch
        {
            // Ignore errors in the error handler to prevent cascading failures
        }
    }

    /// <summary>
    /// Disposes of the logging publisher and its targets if applicable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
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
                catch (Exception ex)
                {
                    // Log disposal errors to debug output
                    Debug.WriteLine(
                        $"ERROR disposing logging target: {ex.Message}");
                }
            }

            _targets.Clear();
        }
        catch (Exception ex)
        {
            // Log final disposal errors to debug output
            Debug.WriteLine(
                $"ERROR during NLogixDistributor disposal: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a diagnostic report about the publisher's state.
    /// </summary>
    /// <returns>A string containing diagnostic information.</returns>
    public override string ToString()
        => $"[NLogixDistributor Stats - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]" + Environment.NewLine +
           $"- USER: {Environment.UserName}" + Environment.NewLine +
           $"- Active Targets: {_targets.Count}" + Environment.NewLine +
           $"- Entries Published: {TotalEntriesPublished:N0}" + Environment.NewLine +
           $"- Target Operations: {TotalTargetInvocations:N0}" + Environment.NewLine +
           $"- Errors: {TotalPublishErrors:N0}" + Environment.NewLine +
           $"- Disposed: {_isDisposed != 0}" + Environment.NewLine;

    #endregion Public Methods
}
