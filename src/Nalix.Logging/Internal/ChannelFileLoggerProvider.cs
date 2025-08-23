// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Sinks.File;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nalix.Logging.Internal;

/// <summary>
/// High-throughput file logger provider using <see cref="Channel{T}"/> + batching.
/// Drop-in alternative to <c>FileLoggerProvider</c> optimized for low contention and fewer syscalls.
/// </summary>
/// <remarks>
/// - Single consumer background task reads from a bounded channel.
/// - Producers use TryWrite (drop) or WriteAsync (block) based on <see cref="FileLogOptions.BlockWhenQueueFull"/>.
/// - Batching: flush by item count or elapsed time, whichever comes first.
/// - Adaptive flush interval can be toggled via constructor parameters.
/// </remarks>
[DebuggerDisplay("Queued={QueuedEntryCount}, Written={TotalEntriesWritten}, Dropped={EntriesDroppedCount}")]
internal sealed class ChannelFileLoggerProvider : System.IDisposable
{
    #region Fields

    private readonly Channel<System.String> _channel;
    private readonly ChannelWriter<System.String> _writer;
    private readonly ChannelReader<System.String> _reader;

    private readonly ChannelFileWriter _fileWriter;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumerTask;

    private readonly System.Int32 _maxQueueSize;
    private readonly System.Boolean _blockWhenFull;

    private readonly System.Int32 _batchSize;
    private readonly System.TimeSpan _maxBatchDelay;
    private readonly System.Boolean _adaptiveFlush;

    private System.Boolean _disposed;
    private System.Int64 _totalEntriesWritten;
    private System.Int64 _entriesDroppedCount;
    private System.Int32 _queued; // approximate queued (interlocked)

    #endregion

    #region Ctor & Options

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelFileLoggerProvider"/>.
    /// </summary>
    /// <param name="options">File logger options (reuses existing <see cref="FileLogOptions"/>).</param>
    /// <param name="batchSize">Max entries per batch before a flush (default: 256).</param>
    /// <param name="maxBatchDelay">Max time to wait before flushing a partial batch (default: options.FlushInterval or 1s).</param>
    /// <param name="adaptiveFlush">Enable adaptive flush based on incoming rate (default: true).</param>
    public ChannelFileLoggerProvider(
        FileLogOptions options,
        System.Int32 batchSize = 256,
        System.TimeSpan? maxBatchDelay = null,
        System.Boolean adaptiveFlush = true)
    {
        Options = options ?? throw new System.ArgumentNullException(nameof(options));

        _maxQueueSize = System.Math.Max(1, options.MaxQueueSize);
        _blockWhenFull = options.BlockWhenQueueFull;
        _batchSize = System.Math.Max(1, batchSize);
        _maxBatchDelay = maxBatchDelay ?? options.FlushInterval;
        if (_maxBatchDelay <= System.TimeSpan.Zero)
        {
            _maxBatchDelay = System.TimeSpan.FromSeconds(1);
        }

        _adaptiveFlush = adaptiveFlush;

        // Bounded channel, single consumer, many producers
        var channelOptions = new BoundedChannelOptions(_maxQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = _blockWhenFull ? BoundedChannelFullMode.Wait : BoundedChannelFullMode.DropNewest
        };
        _channel = Channel.CreateBounded<System.String>(channelOptions);
        _writer = _channel.Writer;
        _reader = _channel.Reader;

        _fileWriter = new ChannelFileWriter(this);
        _consumerTask = Task.Factory.StartNew(
            ConsumeLoopAsync,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    #endregion

    #region Public API & Stats

    /// <summary>
    /// Gets the options used by this provider.
    /// </summary>
    public FileLogOptions Options { get; }

    /// <summary>
    /// Approximate number of entries waiting to be written.
    /// </summary>
    public System.Int32 QueuedEntryCount => System.Math.Max(0, Volatile.Read(ref _queued));

    /// <summary>
    /// Total entries written (since start).
    /// </summary>
    public System.Int64 TotalEntriesWritten => Interlocked.Read(ref _totalEntriesWritten);

    /// <summary>
    /// Entries dropped due to capacity (when non-blocking).
    /// </summary>
    public System.Int64 EntriesDroppedCount => Interlocked.Read(ref _entriesDroppedCount);

    /// <summary>
    /// Enqueue a formatted log message.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void WriteEntry(System.String message)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (System.String.IsNullOrEmpty(message))
        {
            return;
        }

        if (_blockWhenFull)
        {
            // Backpressure: block producers
            _ = _writer.WriteAsync(message, _cts.Token).AsTask().ConfigureAwait(false);
            _ = Interlocked.Increment(ref _queued);
        }
        else
        {
            if (_writer.TryWrite(message))
            {
                _ = Interlocked.Increment(ref _queued);
            }
            else
            {
                _ = Interlocked.Increment(ref _entriesDroppedCount);
            }
        }
    }

    /// <summary>
    /// Force a flush of current buffers to disk.
    /// </summary>
    public void FlushQueue() => _fileWriter.Flush();

    /// <summary>
    /// Diagnostic snapshot.
    /// </summary>
    public System.String GetDiagnosticInfo()
    {
        return $"ChannelFileLoggerProvider [UTC: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]"
             + System.Environment.NewLine + $"- User: {System.Environment.UserName}"
             + System.Environment.NewLine + $"- Log File: {System.IO.Path.Combine(Options.LogDirectory, Options.LogFileName)}"
             + System.Environment.NewLine + $"- Written: {TotalEntriesWritten:N0}"
             + System.Environment.NewLine + $"- Dropped: {EntriesDroppedCount:N0}"
             + System.Environment.NewLine + $"- Queue: ~{QueuedEntryCount:N0}/{_maxQueueSize}";
    }

    #endregion

    #region Consumer Loop

    private async Task ConsumeLoopAsync()
    {
        var batch = new List<System.String>(_batchSize);
        var sw = new Stopwatch();
        sw.Start();
        System.TimeSpan currentDelay = _maxBatchDelay;

        try
        {
            while (await _reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                // Read at least one item (to start a batch window)
                if (_reader.TryRead(out var first))
                {
                    batch.Add(first);
                    _ = Interlocked.Decrement(ref _queued);
                }
                else
                {
                    continue;
                }

                var batchStartTicks = sw.ElapsedTicks;
                // Accumulate until batch size or time window exceeded
                while (batch.Count < _batchSize)
                {
                    // Break if time window exceeded
                    if (sw.Elapsed - System.TimeSpan.FromTicks(batchStartTicks) >= currentDelay)
                    {
                        break;
                    }

                    if (_reader.TryRead(out var item))
                    {
                        batch.Add(item);
                        _ = Interlocked.Decrement(ref _queued);
                    }
                    else
                    {
                        // No data right now, small delay to yield
                        await Task.Yield();
                        break;
                    }
                }

                // Write batch
                _fileWriter.WriteBatch(batch);
                _ = Interlocked.Add(ref _totalEntriesWritten, batch.Count);
                batch.Clear();

                // Adaptive delay: if we consistently fill the batch, shrink delay to reduce latency;
                // if batches are tiny, increase delay a bit to improve throughput.
                if (_adaptiveFlush)
                {
                    currentDelay = batch.Count >= _batchSize - 1
                        ? System.TimeSpan.FromMilliseconds(System.Math
                                                      .Max(1, currentDelay.TotalMilliseconds * 0.75))
                        : System.TimeSpan.FromMilliseconds(System.Math
                                                      .Min(5000, System.Math
                                                      .Max(1, currentDelay.TotalMilliseconds * 1.25)));
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            // draining below
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"Channel consumer error: {ex}");
        }
        finally
        {
            // Drain remaining messages after cancellation
            while (_reader.TryRead(out var msg))
            {
                batch.Add(msg);
                if (batch.Count >= _batchSize)
                {
                    _fileWriter.WriteBatch(batch);
                    _ = Interlocked.Add(ref _totalEntriesWritten, batch.Count);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                _fileWriter.WriteBatch(batch);
                _ = Interlocked.Add(ref _totalEntriesWritten, batch.Count);
                batch.Clear();
            }
        }
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _writer.Complete();
            _cts.Cancel();

            try
            {
                _ = _consumerTask.Wait(System.TimeSpan.FromSeconds(3));
            }
            catch
            {
                // ignore
            }

            _fileWriter.Flush();
            _fileWriter.Dispose();
            _cts.Dispose();
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"Dispose error: {ex}");
        }
        System.GC.SuppressFinalize(this);
    }

    #endregion
}