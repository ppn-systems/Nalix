// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Interop;
using Nalix.Logging.Options;

namespace Nalix.Logging.Sinks;

/// <summary>
/// A logging target that buffers log messages and periodically writes them to the console.
/// This approach improves performance by reducing console I/O operations when logging frequently.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Buffered={_count}, Max={_maxBufferSize}, Disposed={_disposed}")]
public sealed class BatchConsoleLogTarget : ILoggerTarget, System.IDisposable
{
    #region Fields

    private readonly System.Collections.Concurrent.ConcurrentQueue<LogEntry> _queue = new();
    private readonly ConsoleLogTarget _consoleLoggingTarget;
    private readonly System.Threading.Timer _flushTimer;
    private readonly System.Int32 _maxBufferSize;
    private readonly System.Boolean _autoFlush;

    private System.Int32 _count;
    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchConsoleLogTarget"/> class using an existing <see cref="ConsoleLogTarget"/>.
    /// </summary>
    /// <param name="consoleLoggingTarget">The underlying console logging target to write entries to.</param>
    /// <param name="flushInterval">The time interval between automatic buffer flushes.</param>
    /// <param name="maxBufferSize">The maximum number of log entries before triggering a flush.</param>
    /// <param name="autoFlush">Determines whether to automatically flush when the buffer is full.</param>
    public BatchConsoleLogTarget(
        ConsoleLogTarget consoleLoggingTarget,
        System.TimeSpan flushInterval,
        System.Int32 maxBufferSize = 100,
        System.Boolean autoFlush = true)
    {
        System.ArgumentNullException.ThrowIfNull(consoleLoggingTarget);
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBufferSize);
        System.ArgumentOutOfRangeException.ThrowIfNegative(flushInterval.Ticks);

        _consoleLoggingTarget = consoleLoggingTarget;
        _maxBufferSize = maxBufferSize;
        _autoFlush = autoFlush;

        _flushTimer = new System.Threading.Timer(_ => Flush(), null, flushInterval, flushInterval);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchConsoleLogTarget"/> class using configured options.
    /// </summary>
    /// <param name="options">The configuration options for the <see cref="BatchConsoleLogTarget"/>.</param>
    public BatchConsoleLogTarget(BatchConsoleLogOptions options)
        : this(
            consoleLoggingTarget: new ConsoleLogTarget(new Nalix.Logging.Core.NLogixFormatter(options.EnableColors)),
            flushInterval: options.FlushInterval,
            maxBufferSize: options.MaxBufferSize,
            autoFlush: options.AutoFlushOnFull)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchConsoleLogTarget"/> class using an options builder.
    /// </summary>
    /// <param name="configureOptions">The action used to configure the options.</param>
    public BatchConsoleLogTarget(System.Action<BatchConsoleLogOptions> configureOptions)
        : this(ConfigureOptions(configureOptions))
    {
    }

    /// <summary>
    /// Basic constructor that initializes a new instance of the <see cref="BatchConsoleLogTarget"/> class with default options.
    /// </summary>
    public BatchConsoleLogTarget()
        : this(new BatchConsoleLogOptions())
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Publishes a log entry to the buffer. If <see cref="_autoFlush"/> is enabled
    /// and the buffer exceeds <see cref="_maxBufferSize"/>, the buffer is flushed asynchronously.
    /// </summary>
    /// <param name="logMessage">The log entry to publish.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
    /// Flushes the current log buffer to the underlying console logging target.
    /// This method is thread-safe and can be called manually or triggered automatically.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Flush()
    {
        if (_disposed)
        {
            return;
        }

        using (ConsoleGate.Shared())
        {
            while (_queue.TryDequeue(out LogEntry log))
            {
                _consoleLoggingTarget.Publish(log);
            }
            _ = System.Threading.Interlocked.Exchange(ref _count, 0);
        }
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Configures the <see cref="BatchConsoleLogOptions"/> by invoking the provided action.
    /// </summary>
    /// <param name="configureOptions">The action used to configure the options.</param>
    /// <returns>The configured <see cref="BatchConsoleLogOptions"/>.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static BatchConsoleLogOptions ConfigureOptions(System.Action<BatchConsoleLogOptions> configureOptions)
    {
        BatchConsoleLogOptions options = new();
        configureOptions(options);
        return options;
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the <see cref="BatchConsoleLogTarget"/> instance.
    /// Flushes any remaining logs in the buffer before shutting down.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        this.Flush();

        _flushTimer.Dispose();
        // _consoleLoggingTarget does not hold unmanaged resources; no dispose required.

        _disposed = true;
    }

    #endregion IDisposable
}
