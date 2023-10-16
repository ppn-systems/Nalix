using Nalix.Common.Logging;
using Nalix.Logging.Options;

namespace Nalix.Logging.Targets;

/// <summary>
/// A logging target that buffers log messages and periodically writes them to a file.
/// This approach improves performance by reducing I/O operations when logging frequently.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Buffered={_count}, Max={_maxBufferSize}, Disposed={_disposed}")]
public sealed class BatchFileLogTarget : ILoggerTarget, System.IDisposable
{
    #region Fields

    private readonly System.Collections.Concurrent.ConcurrentQueue<LogEntry> _queue = new();
    private readonly FileLogTarget _fileLoggingTarget;
    private readonly System.Threading.Timer _flushTimer;
    private readonly System.Int32 _maxBufferSize;
    private readonly System.Boolean _autoFlush;

    private System.Int32 _count;
    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with custom file logging target.
    /// </summary>
    /// <param name="fileLoggingTarget">An externally created <see cref="FileLogTarget"/> to use for writing logs.</param>
    /// <param name="flushInterval">The time interval between automatic buffer flushes.</param>
    /// <param name="maxBufferSize">The maximum number of log entries before triggering a flush.</param>
    /// <param name="autoFlush">Determines whether to automatically flush when the buffer is full.</param>
    public BatchFileLogTarget(
        FileLogTarget fileLoggingTarget, System.TimeSpan flushInterval,
        System.Int32 maxBufferSize = 100, System.Boolean autoFlush = true)
    {
        System.ArgumentNullException.ThrowIfNull(fileLoggingTarget);
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBufferSize);
        System.ArgumentOutOfRangeException.ThrowIfNegative(flushInterval.Ticks);

        _autoFlush = autoFlush;
        _maxBufferSize = maxBufferSize;
        _fileLoggingTarget = fileLoggingTarget;

        _flushTimer = new System.Threading.Timer(_ => Flush(), null, flushInterval, flushInterval);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class using a configured <see cref="BatchFileLogOptions"/>.
    /// </summary>
    /// <param name="options">The configuration options for the <see cref="BatchFileLogOptions"/>.</param>
    public BatchFileLogTarget(BatchFileLogOptions options)
        : this(new FileLogTarget(options.FileLoggerOptions),
               options.FlushInterval, options.MaxBufferSize, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class using a configured <see cref="BatchFileLogOptions"/>.
    /// </summary>
    /// <param name="options">The configuration options for the <see cref="BatchFileLogOptions"/>.</param>
    public BatchFileLogTarget(System.Action<BatchFileLogOptions> options)
        : this(ConfigureOptions(options))
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Publishes a log entry to the buffer. If <see cref="_autoFlush"/> is enabled
    /// and the buffer exceeds <see cref="_maxBufferSize"/>, the buffer is flushed immediately.
    /// </summary>
    /// <param name="logMessage">The log entry to publish.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Publish(LogEntry logMessage)
    {
        if (_disposed)
        {
            return;
        }

        _queue.Enqueue(logMessage);
        System.Int32 currentCount = System.Threading.Interlocked.Increment(ref _count);

        if (_autoFlush && currentCount >= _maxBufferSize)
        {
            _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(_ => Flush(), null);
        }
    }

    /// <summary>
    /// Flushes the current log buffer to the underlying file logging target.
    /// This method is thread-safe and can be called manually or triggered automatically.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Flush()
    {
        if (_disposed)
        {
            return;
        }

        while (_queue.TryDequeue(out LogEntry log))
        {
            _fileLoggingTarget.Publish(log);
        }

        _ = System.Threading.Interlocked.Exchange(ref _count, 0);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Configures the <see cref="BatchFileLogOptions"/> by invoking the provided action.
    /// </summary>
    /// <param name="configureOptions">The action used to configure the options.</param>
    /// <returns>The configured <see cref="BatchFileLogOptions"/>.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static BatchFileLogOptions ConfigureOptions(System.Action<BatchFileLogOptions> configureOptions)
    {
        BatchFileLogOptions options = new();
        configureOptions(options);
        return options;
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the <see cref="BatchFileLogTarget"/> instance.
    /// Flushes any remaining logs in the buffer before shutting down.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        this.Flush();

        _flushTimer.Dispose();
        _fileLoggingTarget.Dispose();

        _disposed = true;
    }

    #endregion IDisposable
}
