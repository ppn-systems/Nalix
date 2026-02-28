// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Environment;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Logging.Configuration;


#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.File;

/// <summary>
/// High-throughput file logger provider using <see cref="System.Threading.Channels.Channel{T}"/> + batching.
/// Drop-in alternative to <c>FileLoggerProvider</c> optimized for low contention and fewer syscalls.
/// </summary>
/// <remarks>
/// - Single consumer background task reads from a bounded channel.
/// - Producers use TryWrite (drop) or WriteAsync (block) based on <see cref="FileLogOptions.BlockWhenQueueFull"/>.
/// - Batching: flush by item count or elapsed time, whichever comes first.
/// - Adaptive flush interval can be toggled via constructor parameters.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("Queued={QueuedEntryCount}, Written={TotalEntriesWritten}, Dropped={EntriesDroppedCount}")]
internal sealed class FileLoggerProvider : System.IDisposable
{
    #region Fields

    private readonly System.Threading.Channels.Channel<System.String> _channel;
    private readonly System.Threading.Channels.ChannelWriter<System.String> _writer;
    private readonly System.Threading.Channels.ChannelReader<System.String> _reader;

    private readonly FileWriter _fileWriter;
    private readonly IWorkerHandle? _workerHandle;
    private readonly System.Threading.CancellationTokenSource _cts = new();

    private readonly System.Int32 _maxQueueSize;
    private readonly System.Boolean _blockWhenFull;

    private readonly System.Int32 _batchSize;
    private readonly ILoggerFormatter _formatter;
    private readonly System.Boolean _adaptiveFlush;
    private readonly System.TimeSpan _maxBatchDelay;

    private System.Int32 _queued;
    private System.Boolean _disposed;
    private System.Int64 _totalEntriesWritten;
    private System.Int64 _entriesDroppedCount;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggerProvider"/>.
    /// </summary>
    /// <param name="formatter">The formatter used to format log messages.</param>
    /// <param name="options">File logger options (reuses existing <see cref="FileLogOptions"/>).</param>
    /// <param name="batchSize">Max entries per batch before a flush (default: 256).</param>
    /// <param name="maxBatchDelay">Max time to wait before flushing a partial batch (default: options.FlushInterval or 1s).</param>
    /// <param name="adaptiveFlush">Enable adaptive flush based on incoming rate (default: true).</param>
    public FileLoggerProvider(
        ILoggerFormatter formatter,
        FileLogOptions? options = null,
        System.Int32 batchSize = 256,
        System.TimeSpan? maxBatchDelay = null,
        System.Boolean adaptiveFlush = true)
    {
        Options = options ?? ConfigurationManager.Instance.Get<FileLogOptions>();

        _cts = new();
        _formatter = formatter;
        _adaptiveFlush = adaptiveFlush;
        _batchSize = System.Math.Max(1, batchSize);
        _blockWhenFull = Options.BlockWhenQueueFull;
        _maxBatchDelay = maxBatchDelay ?? Options.FlushInterval;
        _maxQueueSize = System.Math.Max(1, Options.MaxQueueSize);

        if (_maxBatchDelay <= System.TimeSpan.Zero)
        {
            _maxBatchDelay = System.TimeSpan.FromSeconds(1);
        }

        // Bounded channel, single consumer, many producers
        System.Threading.Channels.BoundedChannelOptions channelOptions = new(_maxQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = _blockWhenFull ? System.Threading.Channels.BoundedChannelFullMode.Wait : System.Threading.Channels.BoundedChannelFullMode.DropNewest
        };

        _channel = System.Threading.Channels.Channel.CreateBounded<System.String>(channelOptions);
        _writer = _channel.Writer;
        _reader = _channel.Reader;

        _fileWriter = new FileWriter(this);
        _workerHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
            .ScheduleWorker(
                name: "log.file.worker",
                group: "log",
                work: async (ctx, ct) =>
                {
                    using var linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                    await CONSUME_LOOP_ASYNC(ctx, linkedCts.Token);
                },
                options: new WorkerOptions
                {
                    Tag = "file-consumer",
                    GroupConcurrencyLimit = 1,
                    OnFailed = (st, ex) => System.Diagnostics.Debug.WriteLine($"[LG.WebhookLogger] Worker failed: {st.Name}, {ex.Message}"),
                    OnCompleted = st => System.Diagnostics.Debug.WriteLine($"[LG.WebhookLogger] Worker completed: {st.Name} Runs={st.TotalRuns}"),
                }
            );
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Gets the options used by this provider.
    /// </summary>
    public FileLogOptions Options { get; }

    /// <summary>
    /// Approximate number of entries waiting to be written.
    /// </summary>
    public System.Int32 QueuedEntryCount => System.Math.Max(0, System.Threading.Volatile.Read(ref _queued));

    /// <summary>
    /// Total entries written (since start).
    /// </summary>
    public System.Int64 TotalEntriesWritten => System.Threading.Interlocked.Read(ref _totalEntriesWritten);

    /// <summary>
    /// Entries dropped due to capacity (when non-blocking).
    /// </summary>
    public System.Int64 EntriesDroppedCount => System.Threading.Interlocked.Read(ref _entriesDroppedCount);

    #endregion Properties

    #region APIs

    /// <summary>
    /// Enqueue a formatted log message.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void Enqueue(LogEntry entry)
    {
        System.String message = _formatter.Format(entry);

        if (_disposed || System.String.IsNullOrEmpty(message))
        {
            return;
        }

        if (_blockWhenFull)
        {
            try
            {
                _ = _writer.WriteAsync(message, _cts.Token).AsTask().ConfigureAwait(false);
                _ = System.Threading.Interlocked.Increment(ref _queued);
            }
            catch
            {
                // swallow: don't throw from logger
                _ = System.Threading.Interlocked.Increment(ref _entriesDroppedCount);
            }
        }
        else
        {
            _ = _writer.TryWrite(message) ? System.Threading.Interlocked.Increment(ref _queued)
                                          : System.Threading.Interlocked.Increment(ref _entriesDroppedCount);
        }
    }


    /// <summary>
    /// Force a flush of current buffers to disk.
    /// </summary>
    public void Flush() => _fileWriter.Flush();

    /// <summary>
    /// Diagnostic snapshot.
    /// </summary>
    public System.String GetDiagnostics()
    {
        return $"ChannelFileLoggerProvider [UTC: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]"
             + System.Environment.NewLine + $"- USER: {System.Environment.UserName}"
             + System.Environment.NewLine + $"- Log File: {System.IO.Path.Combine(Directories.LogsDirectory, Options.LogFileName)}"
             + System.Environment.NewLine + $"- Written: {TotalEntriesWritten:N0}"
             + System.Environment.NewLine + $"- Dropped: {EntriesDroppedCount:N0}"
             + System.Environment.NewLine + $"- Queue: ~{QueuedEntryCount:N0}/{_maxQueueSize}";
    }

    #endregion APIs

    #region Private Methods

    private async System.Threading.Tasks.Task CONSUME_LOOP_ASYNC(IWorkerContext ctx, System.Threading.CancellationToken ct)
    {
        System.Collections.Generic.List<System.String> batch = new(_batchSize);
        System.Diagnostics.Stopwatch sw = new();
        sw.Start();
        System.TimeSpan currentDelay = _maxBatchDelay;

        try
        {
            while (await _reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                // Đọc 1 item đầu tiên
                if (_reader.TryRead(out var first))
                {
                    batch.Add(first);
                    _ = System.Threading.Interlocked.Decrement(ref _queued);
                }
                else
                {
                    continue;
                }

                var batchStartTicks = sw.ElapsedTicks;
                while (batch.Count < _batchSize)
                {
                    if (sw.Elapsed - System.TimeSpan.FromTicks(batchStartTicks) >= currentDelay)
                    {
                        break;
                    }

                    if (_reader.TryRead(out var item))
                    {
                        batch.Add(item);
                        _ = System.Threading.Interlocked.Decrement(ref _queued);
                    }
                    else
                    {
                        await System.Threading.Tasks.Task.Yield();
                        break;
                    }
                }

                _fileWriter.WriteBatch(batch);
                _ = System.Threading.Interlocked.Add(ref _totalEntriesWritten, batch.Count);

                // Update progress & heartbeat
                ctx.Advance(batch.Count, "File logs written");
                ctx.Beat();

                batch.Clear();

                if (_adaptiveFlush)
                {
                    currentDelay = batch.Count >= _batchSize - 1
                        ? System.TimeSpan.FromMilliseconds(System.Math.Max(1, currentDelay.TotalMilliseconds * 0.75))
                        : System.TimeSpan.FromMilliseconds(System.Math.Min(5000, System.Math.Max(1, currentDelay.TotalMilliseconds * 1.25)));
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            // draining below
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Channel consumer error: {ex}");
        }
        finally
        {
            while (_reader.TryRead(out var msg))
            {
                batch.Add(msg);
                if (batch.Count >= _batchSize)
                {
                    _fileWriter.WriteBatch(batch);
                    _ = System.Threading.Interlocked.Add(ref _totalEntriesWritten, batch.Count);
                    ctx.Advance(batch.Count, "File logs written (shutdown)");
                    ctx.Beat();
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                _fileWriter.WriteBatch(batch);
                _ = System.Threading.Interlocked.Add(ref _totalEntriesWritten, batch.Count);
                ctx.Advance(batch.Count, "File logs written (shutdown)");
                ctx.Beat();
                batch.Clear();
            }
        }
    }


    #endregion Private Methods

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

            if (_workerHandle != null)
            {
                InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                        .CancelWorker(_workerHandle.Id);
            }

            _fileWriter.Flush();
            _fileWriter.Dispose();
            _cts.Dispose();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dispose error: {ex}");
        }
        System.GC.SuppressFinalize(this);
    }

    #endregion Dispose
}