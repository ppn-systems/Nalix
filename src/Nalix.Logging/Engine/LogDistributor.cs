using Nalix.Common.Logging;

namespace Nalix.Logging.Engine;

/// <summary>
/// High-performance publisher that dispatches log entries to registered logging targets.
/// </summary>
public sealed class LogDistributor : ILogDistributor
{
    #region Fields

    // Use a dummy value (0) for dictionary entries as we only care about the keys
    private const System.Byte DummyValue = 0;

    // Using a concurrent dictionary for thread-safe operations on targets
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ILoggerTarget, System.Byte> _targets = new();

    // Track disposed state in a thread-safe way
    private System.Int32 _isDisposed;

    private System.Int64 _entriesDistributor;
    private System.Int64 _targetsProcessed;
    private System.Int64 _publishErrorCount;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total ProtocolType of log entries that have been published.
    /// </summary>
    public System.Int64 EntriesDistributor
        => System.Threading.Interlocked.Read(ref _entriesDistributor);

    /// <summary>
    /// Gets the total ProtocolType of target publish operations performed.
    /// </summary>
    public System.Int64 TargetsProcessed
        => System.Threading.Interlocked.Read(ref _targetsProcessed);

    /// <summary>
    /// Gets the ProtocolType of errors that occurred during publish operations.
    /// </summary>
    public System.Int64 PublishErrorCount
        => System.Threading.Interlocked.Read(ref _publishErrorCount);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Publishes a log entry to all registered logging targets.
    /// </summary>
    /// <param name="entry">The log entry to be published.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if entry is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Publish(LogEntry? entry)
    {
        if (entry == null)
        {
            throw new System.ArgumentNullException(nameof(entry));
        }

        // Quick check for disposed state
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(LogDistributor));

        // Increment the published entries counter
        _ = System.Threading.Interlocked.Increment(ref _entriesDistributor);

        // Optimize for the common case of a single target
        if (_targets.Count == 1)
        {
            ILoggerTarget target = System.Linq.Enumerable.First(_targets.Keys);
            try
            {
                target.Publish(entry.Value);
                _ = System.Threading.Interlocked.Increment(ref _targetsProcessed);
            }
            catch (System.Exception ex)
            {
                // Count the error but continue operation
                _ = System.Threading.Interlocked.Increment(ref _publishErrorCount);
                HandleTargetError(target, ex, entry.Value);
            }
            return;
        }

        // For multiple targets, publish to each
        foreach (ILoggerTarget target in _targets.Keys)
        {
            try
            {
                target.Publish(entry.Value);
                _ = System.Threading.Interlocked.Increment(ref _targetsProcessed);
            }
            catch (System.Exception ex)
            {
                // Count the error but continue with other targets
                _ = System.Threading.Interlocked.Increment(ref _publishErrorCount);
                HandleTargetError(target, ex, entry.Value);
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
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(LogDistributor));

        // For simplicity and performance, use Task.Run only when there are multiple targets
        // Otherwise just do it synchronously to avoid task allocation overhead
        if (_targets.Count <= 1)
        {
            Publish(entry.Value);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }

        return new System.Threading.Tasks.ValueTask(
            System.Threading.Tasks.Task.Run(() => Publish(entry.Value))
        );
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    /// <param name="target">The logging target to add.</param>
    /// <returns>The current instance of <see cref="ILogDistributor"/>, allowing method chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    public ILogDistributor AddTarget(ILoggerTarget target)
    {
        System.ArgumentNullException.ThrowIfNull(target);
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(LogDistributor));

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
    public System.Boolean RemoveTarget(ILoggerTarget target)
    {
        System.ArgumentNullException.ThrowIfNull(target);
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(LogDistributor));

        return _targets.TryRemove(target, out _);
    }

    /// <summary>
    /// Handles errors that occur when publishing to a target.
    /// </summary>
    /// <param name="target">The target that caused the error.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="entry">The log entry being published.</param>
    private static void HandleTargetError(ILoggerTarget target, System.Exception exception, LogEntry entry)
    {
        try
        {
            // Log to debug output at minimum
            System.Diagnostics.Debug.WriteLine(
                $"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Error publishing to " +
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
                        $"Error disposing logging target: {ex.Message}");
                }
            }

            _targets.Clear();
        }
        catch (System.Exception ex)
        {
            // Log final disposal errors to debug output
            System.Diagnostics.Debug.WriteLine(
                $"Error during LogDistributor disposal: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a diagnostic report about the publisher's state.
    /// </summary>
    /// <returns>A string containing diagnostic information.</returns>
    public override System.String ToString()
        => $"[LogDistributor Stats - {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]" + System.Environment.NewLine +
           $"- User: {System.Environment.UserName}" + System.Environment.NewLine +
           $"- Active Targets: {_targets.Count}" + System.Environment.NewLine +
           $"- Entries Published: {EntriesDistributor:N0}" + System.Environment.NewLine +
           $"- Target Operations: {TargetsProcessed:N0}" + System.Environment.NewLine +
           $"- Errors: {PublishErrorCount:N0}" + System.Environment.NewLine +
           $"- Disposed: {_isDisposed != 0}" + System.Environment.NewLine;

    #endregion Public Methods
}