// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;

namespace Nalix.Logging.Core;

/// <summary>
/// High-performance publisher that dispatches log entries to registered logging targets.
/// </summary>
[System.Diagnostics.DebuggerDisplay("{ToString(),nq}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class NLogixDistributor : ILogDistributor
{
    #region Fields

    // Use a dummy value (0) for dictionary entries as we only care about the keys
    private const System.Byte DummyValue = 0;

    // Using a concurrent dictionary for thread-safe operations on targets
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ILoggerTarget, System.Byte> _targets = new();

    // Track disposed state in a thread-safe way
    private System.Int32 _isDisposed;

    private System.Int64 _totalEntriesPublished;
    private System.Int64 _totalTargetInvocations;
    private System.Int64 _totalPublishErrors;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the ProtocolType of errors that occurred during publish operations.
    /// </summary>
    public System.Int64 TotalPublishErrors
        => System.Threading.Interlocked.Read(ref _totalPublishErrors);

    /// <summary>
    /// Gets the total ProtocolType of log entries that have been published.
    /// </summary>
    public System.Int64 TotalEntriesPublished
        => System.Threading.Interlocked.Read(ref _totalEntriesPublished);

    /// <summary>
    /// Gets the total ProtocolType of target publish operations performed.
    /// </summary>
    public System.Int64 TotalTargetInvocations
        => System.Threading.Interlocked.Read(ref _totalTargetInvocations);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Publishes a log entry to all registered logging targets.
    /// </summary>
    /// <param name="entry">The log entry to be published.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if entry is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Publish(LogEntry? entry)
    {
        if (entry == null)
        {
            throw new System.ArgumentNullException(nameof(entry));
        }

        // Quick check for disposed state
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        // Increment the published entries counter
        _ = System.Threading.Interlocked.Increment(ref _totalEntriesPublished);
        System.Int32 count = _targets.Count;

        // Fast path: no targets
        if (count == 0)
        {
            return;
        }

        // Optimize for the common case of a single target
        if (count == 1)
        {
            foreach (System.Collections.Generic.KeyValuePair<ILoggerTarget, System.Byte> kvp in _targets)
            {
                try
                {
                    kvp.Key.Publish(entry.Value);
                    _ = System.Threading.Interlocked.Increment(ref _totalTargetInvocations);
                }
                catch (System.Exception ex)
                {
                    // Count the error but continue operation
                    _ = System.Threading.Interlocked.Increment(ref _totalPublishErrors);
                    HandleTargetError(kvp.Key, ex, entry.Value);
                }
                return; // Only one target, exit after processing
            }
        }

        // Multiple targets: iterate and publish to each
        foreach (System.Collections.Generic.KeyValuePair<ILoggerTarget, System.Byte> kvp in _targets)
        {
            try
            {
                kvp.Key.Publish(entry.Value);
                _ = System.Threading.Interlocked.Increment(ref _totalTargetInvocations);
            }
            catch (System.Exception ex)
            {
                // Count the error but continue operation
                _ = System.Threading.Interlocked.Increment(ref _totalPublishErrors);
                HandleTargetError(kvp.Key, ex, entry.Value);
            }
        }
    }

    /// <summary>
    /// Publishes a log entry asynchronously to all registered logging targets.
    /// </summary>
    /// <param name="entry">The log entry to be published.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if entry is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    public System.Threading.Tasks.ValueTask PublishAsync(LogEntry? entry)
    {
        if (entry == null)
        {
            throw new System.ArgumentNullException(nameof(entry));
        }

        // Quick check for disposed state
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        System.Int32 count = _targets.Count;

        // Fast path: no targets
        if (count == 0)
        {
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }

        // Optimize for the common case of a single target
        if (count == 1)
        {
            foreach (System.Collections.Generic.KeyValuePair<ILoggerTarget, System.Byte> kvp in _targets)
            {
                try
                {
                    kvp.Key.Publish(entry.Value);
                    _ = System.Threading.Interlocked.Increment(ref _totalTargetInvocations);
                }
                catch (System.Exception ex)
                {
                    // Count the error but continue operation
                    _ = System.Threading.Interlocked.Increment(ref _totalPublishErrors);
                    HandleTargetError(kvp.Key, ex, entry.Value);
                }

                // Only one target, exit after processing
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }
        }

        // For best performance, publish synchronously and let targets handle async internally
        Publish(entry);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    /// <param name="target">The logging target to add.</param>
    /// <returns>The current instance of <see cref="ILogDistributor"/>, allowing method chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public ILogDistributor RegisterTarget([System.Diagnostics.CodeAnalysis.NotNull] ILoggerTarget target)
    {
        System.ArgumentNullException.ThrowIfNull(target);
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        _ = _targets.TryAdd(target, DummyValue);
        return this;
    }

    /// <summary>
    /// Removes a logging target from the publisher.
    /// </summary>
    /// <param name="target">The logging target to remove.</param>
    /// <returns><c>true</c> if the target was successfully removed; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean UnregisterTarget([System.Diagnostics.CodeAnalysis.NotNull] ILoggerTarget target)
    {
        System.ArgumentNullException.ThrowIfNull(target);
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(NLogixDistributor));

        return _targets.TryRemove(target, out _);
    }

    /// <summary>
    /// Handles errors that occur when publishing to a target.
    /// </summary>
    /// <param name="target">The target that caused the error.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="entry">The log entry being published.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void HandleTargetError(ILoggerTarget target, System.Exception exception, LogEntry entry)
    {
        try
        {
            // Log to debug output at minimum
            System.Diagnostics.Debug.WriteLine(
                $"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR publishing to " +
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Dispose()
    {
        // Thread-safe disposal check
        if (System.Threading.Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            // Dispose each target if it implements IDisposable
            foreach (System.IDisposable target in
                System.Linq.Enumerable.OfType<System.IDisposable>(_targets.Keys))
            {
                try
                {
                    target.Dispose();
                }
                catch (System.Exception ex)
                {
                    // Log disposal errors to debug output
                    System.Diagnostics.Debug.WriteLine(
                        $"ERROR disposing logging target: {ex.Message}");
                }
            }

            _targets.Clear();
        }
        catch (System.Exception ex)
        {
            // Log final disposal errors to debug output
            System.Diagnostics.Debug.WriteLine(
                $"ERROR during NLogixDistributor disposal: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a diagnostic report about the publisher's state.
    /// </summary>
    /// <returns>A string containing diagnostic information.</returns>
    public override System.String ToString()
        => $"[NLogixDistributor Stats - {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]" + System.Environment.NewLine +
           $"- USER: {System.Environment.UserName}" + System.Environment.NewLine +
           $"- Active Targets: {_targets.Count}" + System.Environment.NewLine +
           $"- Entries Published: {TotalEntriesPublished:N0}" + System.Environment.NewLine +
           $"- Target Operations: {TotalTargetInvocations:N0}" + System.Environment.NewLine +
           $"- Errors: {TotalPublishErrors:N0}" + System.Environment.NewLine +
           $"- Disposed: {_isDisposed != 0}" + System.Environment.NewLine;

    #endregion Public Methods
}