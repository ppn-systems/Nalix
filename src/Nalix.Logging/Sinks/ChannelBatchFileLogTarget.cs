// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Options;
using System.Threading.Channels;

namespace Nalix.Logging.Sinks;

/// <summary>
/// High-performance logging target that uses System.Threading.Channels for efficient batching.
/// This approach provides better throughput and lower latency compared to traditional queue-based approaches.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Channel={_channel.Reader.Count}, Max={_maxBufferSize}, Disposed={_disposed}")]
public sealed class ChannelBatchFileLogTarget : ILoggerTarget, System.IDisposable
{
    #region Fields

    private readonly Channel<LogEntry> _channel;
    private readonly FileLogTarget _fileLoggingTarget;
    private readonly System.Threading.CancellationTokenSource _cts;
    private readonly System.Threading.Tasks.Task _processingTask;
    private readonly System.Int32 _maxBufferSize;
    private readonly System.TimeSpan _flushInterval;

    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelBatchFileLogTarget"/> class with custom file logging target.
    /// </summary>
    /// <param name="fileLoggingTarget">An externally created <see cref="FileLogTarget"/> to use for writing logs.</param>
    /// <param name="flushInterval">The time interval between automatic buffer flushes.</param>
    /// <param name="maxBufferSize">The maximum number of log entries before triggering a flush.</param>
    public ChannelBatchFileLogTarget(
        FileLogTarget fileLoggingTarget,
        System.TimeSpan flushInterval,
        System.Int32 maxBufferSize = 100)
    {
        System.ArgumentNullException.ThrowIfNull(fileLoggingTarget);
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBufferSize);
        System.ArgumentOutOfRangeException.ThrowIfNegative(flushInterval.Ticks);

        _maxBufferSize = maxBufferSize;
        _flushInterval = flushInterval;
        _fileLoggingTarget = fileLoggingTarget;

        // Create an unbounded channel for better performance
        _channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _cts = new System.Threading.CancellationTokenSource();
        _processingTask = System.Threading.Tasks.Task.Run(() => ProcessLogEntriesAsync(_cts.Token));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelBatchFileLogTarget"/> class using a configured <see cref="BatchFileLogOptions"/>.
    /// </summary>
    /// <param name="options">The configuration options for the <see cref="BatchFileLogOptions"/>.</param>
    public ChannelBatchFileLogTarget(BatchFileLogOptions options)
        : this(new FileLogTarget(options.FileLoggerOptions),
               options.FlushInterval, options.MaxBufferSize)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelBatchFileLogTarget"/> class using a configured <see cref="BatchFileLogOptions"/>.
    /// </summary>
    /// <param name="configureOptions">The configuration options for the <see cref="BatchFileLogOptions"/>.</param>
    public ChannelBatchFileLogTarget(System.Action<BatchFileLogOptions> configureOptions)
        : this(ConfigureOptions(configureOptions))
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Publishes a log entry to the channel for batched processing.
    /// This method is lock-free and highly optimized for minimal latency.
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

        // TryWrite is lock-free and extremely fast for unbounded channels
        _ = _channel.Writer.TryWrite(logMessage);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Configures the <see cref="BatchFileLogOptions"/> by invoking the provided action.
    /// </summary>
    /// <param name="configureOptions">The action used to configure the options.</param>
    /// <returns>The configured <see cref="BatchFileLogOptions"/>.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static BatchFileLogOptions ConfigureOptions(System.Action<BatchFileLogOptions> configureOptions)
    {
        BatchFileLogOptions options = new();
        configureOptions(options);
        return options;
    }

    /// <summary>
    /// Background task that processes log entries from the channel in batches.
    /// Uses intelligent batching with both size and time-based triggers.
    /// </summary>
    private async System.Threading.Tasks.Task ProcessLogEntriesAsync(System.Threading.CancellationToken cancellationToken)
    {
        var batch = new System.Collections.Generic.List<LogEntry>(_maxBufferSize);
        var reader = _channel.Reader;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                batch.Clear();

                // Wait for the first item or timeout
                using var timeoutCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_flushInterval);

                try
                {
                    // Wait for first item
                    if (await reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
                    {
                        // Read as many items as available up to max batch size
                        while (batch.Count < _maxBufferSize && reader.TryRead(out LogEntry entry))
                        {
                            batch.Add(entry);
                        }

                        // Flush the batch
                        FlushBatch(batch);
                    }
                }
                catch (System.OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Timeout occurred, flush any pending items
                    while (reader.TryRead(out LogEntry entry))
                    {
                        batch.Add(entry);
                        if (batch.Count >= _maxBufferSize)
                        {
                            break;
                        }
                    }

                    if (batch.Count > 0)
                    {
                        FlushBatch(batch);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            // Swallow exceptions to prevent task from faulting
            // In production, this could be logged to a fallback mechanism
            System.Diagnostics.Debug.WriteLine($"Log processing error: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            // Final flush on shutdown
            while (reader.TryRead(out LogEntry entry))
            {
                batch.Add(entry);
            }

            if (batch.Count > 0)
            {
                FlushBatch(batch);
            }
        }
    }

    /// <summary>
    /// Flushes a batch of log entries to the file target.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void FlushBatch(System.Collections.Generic.List<LogEntry> batch)
    {
        foreach (LogEntry entry in batch)
        {
            try
            {
                _fileLoggingTarget.Publish(entry);
            }
            catch (System.Exception ex)
            {
                // Swallow to prevent logging failures from crashing the app
                // In production, this could be logged to a fallback mechanism
                System.Diagnostics.Debug.WriteLine($"Failed to publish log entry: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the <see cref="ChannelBatchFileLogTarget"/> instance.
    /// Flushes any remaining logs in the channel before shutting down.
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

        _disposed = true;

        // Complete the channel to signal no more writes
        _channel.Writer.Complete();

        // Cancel the processing task
        _cts.Cancel();

        // Wait for processing to complete with timeout
        try
        {
            _ = _processingTask.Wait(System.TimeSpan.FromSeconds(5));
        }
        catch (System.OperationCanceledException)
        {
            // Ignore timeout or cancellation
        }
        catch (System.TimeoutException)
        {
            // Ignore timeout
        }
        catch (System.Exception ex)
        {
            // Log unexpected exceptions
            System.Diagnostics.Debug.WriteLine($"Error during disposal: {ex.GetType().Name}: {ex.Message}");
        }

        _cts.Dispose();
        _fileLoggingTarget.Dispose();
    }

    #endregion IDisposable
}
