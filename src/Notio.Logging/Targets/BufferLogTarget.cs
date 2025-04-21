using Notio.Common.Logging;
using Notio.Logging.Options;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Notio.Logging.Targets;

/// <summary>
/// A logging target that buffers log messages and periodically writes them to a file.
/// This approach improves performance by reducing I/O operations when logging frequently.
/// </summary>
public sealed class BufferLogTarget : ILoggerTarget, IDisposable
{
    #region Fields

    private readonly FileLogTarget _fileLoggingTarget;
    private readonly ConcurrentQueue<LogEntry> _queue = new();
    private readonly Timer _flushTimer;
    private readonly int _maxBufferSize;
    private readonly bool _autoFlush;

    private int _count;
    private volatile bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferLogTarget"/> class with custom file logging target.
    /// </summary>
    /// <param name="fileLoggingTarget">An externally created <see cref="FileLogTarget"/> to use for writing logs.</param>
    /// <param name="flushInterval">The time interval between automatic buffer flushes.</param>
    /// <param name="maxBufferSize">The maximum number of log entries before triggering a flush.</param>
    /// <param name="autoFlush">Determines whether to automatically flush when the buffer is full.</param>
    public BufferLogTarget(
        FileLogTarget fileLoggingTarget, TimeSpan flushInterval,
        int maxBufferSize = 100, bool autoFlush = true)
    {
        ArgumentNullException.ThrowIfNull(fileLoggingTarget);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBufferSize);
        ArgumentOutOfRangeException.ThrowIfNegative(flushInterval.Ticks);

        _autoFlush = autoFlush;
        _maxBufferSize = maxBufferSize;
        _fileLoggingTarget = fileLoggingTarget;

        _flushTimer = new Timer(_ => Flush(), null, flushInterval, flushInterval);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferLogTarget"/> class using a configured <see cref="BufferLogOptions"/>.
    /// </summary>
    /// <param name="options">The configuration options for the <see cref="BufferLogOptions"/>.</param>
    public BufferLogTarget(BufferLogOptions options)
        : this(new FileLogTarget(options.FileLoggerOptions),
               options.FlushInterval, options.MaxBufferSize, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferLogTarget"/> class using a configured <see cref="BufferLogOptions"/>.
    /// </summary>
    /// <param name="options">The configuration options for the <see cref="BufferLogOptions"/>.</param>
    public BufferLogTarget(Action<BufferLogOptions> options)
        : this(ConfigureOptions(options))
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Publishes a log entry to the buffer. If <see cref="_autoFlush"/> is enabled
    /// and the buffer exceeds <see cref="_maxBufferSize"/>, the buffer is flushed immediately.
    /// </summary>
    /// <param name="logMessage">The log entry to publish.</param>
    public void Publish(LogEntry logMessage)
    {
        if (_disposed) return;

        _queue.Enqueue(logMessage);
        int currentCount = Interlocked.Increment(ref _count);

        if (_autoFlush && currentCount >= _maxBufferSize)
        {
            _ = ThreadPool.UnsafeQueueUserWorkItem(_ => Flush(), null);
        }
    }

    /// <summary>
    /// Flushes the current log buffer to the underlying file logging target.
    /// This method is thread-safe and can be called manually or triggered automatically.
    /// </summary>
    public void Flush()
    {
        if (_disposed) return;

        while (_queue.TryDequeue(out LogEntry log)) _fileLoggingTarget.Publish(log);

        Interlocked.Exchange(ref _count, 0);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Configures the <see cref="BufferLogOptions"/> by invoking the provided action.
    /// </summary>
    /// <param name="configureOptions">The action used to configure the options.</param>
    /// <returns>The configured <see cref="BufferLogOptions"/>.</returns>
    private static BufferLogOptions ConfigureOptions(Action<BufferLogOptions> configureOptions)
    {
        BufferLogOptions options = new();
        configureOptions(options);
        return options;
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases resources used by the <see cref="BufferLogTarget"/> instance.
    /// Flushes any remaining logs in the buffer before shutting down.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        this.Flush();

        _flushTimer.Dispose();
        (_fileLoggingTarget as IDisposable)?.Dispose();

        _disposed = true;
    }

    #endregion
}
