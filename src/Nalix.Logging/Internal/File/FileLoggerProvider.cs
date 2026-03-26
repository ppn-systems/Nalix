// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Environment;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Logging.Configuration;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.File;

/// <summary>
/// High-throughput file logger provider using <see cref="Channel{T}"/> with batching. 
/// Optimized for low contention and minimal system calls.
/// </summary>
[DebuggerDisplay("Queued={QueuedEntryCount}, Written={TotalEntriesWritten}, Dropped={EntriesDroppedCount}")]
internal sealed class FileLoggerProvider : IDisposable, IReportable
{
    #region Fields

    private readonly Channel<LogEntry> _channel;
    private readonly ChannelWriter<LogEntry> _writer;
    private readonly ChannelReader<LogEntry> _reader;
    private readonly FileWriter _fileWriter;
    private readonly IWorkerHandle? _workerHandle;
    private readonly CancellationTokenSource _cts;
    private readonly int _maxQueueSize;
    private readonly bool _blockWhenFull;
    private readonly int _batchSize;
    private readonly ILoggerFormatter _formatter;
    private readonly bool _adaptiveFlush;
    private readonly TimeSpan _maxBatchDelay;
    private int _queued;
    private bool _disposed;
    private long _totalEntriesWritten;
    private long _entriesDroppedCount;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLoggerProvider"/>.
    /// </summary>
    /// <param name="formatter">Formatter to convert <see cref="LogEntry"/> to string.</param>
    /// <param name="options">File logger configuration options.</param>
    /// <param name="batchSize">Maximum entries per batch before flushing (default: 256).</param>
    /// <param name="maxBatchDelay">Maximum time to wait before flushing a non-full batch (default: options.FlushInterval or 1s).</param>
    /// <param name="adaptiveFlush">Enable adaptive flushing based on incoming log rate (default: true).</param>
    public FileLoggerProvider(
        ILoggerFormatter formatter,
        FileLogOptions? options = null,
        int batchSize = 256,
        TimeSpan? maxBatchDelay = null,
        bool adaptiveFlush = true)
    {
        this.Options = options ?? ConfigurationManager.Instance.Get<FileLogOptions>();
        _cts = new CancellationTokenSource();
        _formatter = formatter;
        _adaptiveFlush = adaptiveFlush;
        _batchSize = Math.Max(1, batchSize);
        _blockWhenFull = this.Options.BlockWhenQueueFull;
        _maxBatchDelay = maxBatchDelay ?? this.Options.FlushInterval;
        _maxQueueSize = Math.Max(1, this.Options.MaxQueueSize);

        if (_maxBatchDelay <= TimeSpan.Zero)
        {
            _maxBatchDelay = TimeSpan.FromSeconds(1);
        }

        BoundedChannelOptions channelOptions = new(_maxQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = _blockWhenFull
                ? BoundedChannelFullMode.Wait
                : BoundedChannelFullMode.DropNewest
        };

        _channel = Channel.CreateBounded<LogEntry>(channelOptions);
        _writer = _channel.Writer;
        _reader = _channel.Reader;
        _fileWriter = new FileWriter(this);

        _workerHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
            .ScheduleWorker(
                name: "log.file.worker",
                group: "log",
                work: async (ctx, ct) =>
                {
                    using CancellationTokenSource linkedCts =
                        CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                    await this.CONSUME_LOOP_ASYNC(ctx, linkedCts.Token).ConfigureAwait(false);
                },
                options: new WorkerOptions
                {
                    Tag = "file-consumer",
                    GroupConcurrencyLimit = ConfigurationManager.Instance.Get<NLogixOptions>().GroupConcurrencyLimit,
                    OnFailed = (st, ex) => Debug.WriteLine(
                        $"[LG.FileLogger] Worker failed: {st.Name}, {ex.Message}"),
                    OnCompleted = st => Debug.WriteLine(
                        $"[LG.FileLogger] Worker completed: {st.Name} Runs={st.TotalRuns}"),
                }
            );
    }

    #endregion Constructors

    #region Properties

    /// <summary>Current logger options.</summary>
    public FileLogOptions Options { get; }

    /// <summary>Approximate number of log entries queued.</summary>
    public int QueuedEntryCount => Math.Max(0, Volatile.Read(ref _queued));

    /// <summary>Total number of entries written since startup.</summary>
    public long TotalEntriesWritten => Interlocked.Read(ref _totalEntriesWritten);

    /// <summary>Number of entries dropped due to a full queue.</summary>
    public long EntriesDroppedCount => Interlocked.Read(ref _entriesDroppedCount);

    #endregion Properties

    #region APIs

    /// <summary>
    /// Enqueues a <see cref="LogEntry"/> for file writing. Formatting is handled by the consumer thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void Enqueue(LogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        _ = _writer.TryWrite(entry)
            ? Interlocked.Increment(ref _queued)
            : Interlocked.Increment(ref _entriesDroppedCount);
    }

    /// <summary>
    /// Asynchronously enqueues a log entry; used if <see cref="FileLogOptions.BlockWhenQueueFull"/> is true.
    /// </summary>
    internal async ValueTask EnqueueAsync(LogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await _writer.WriteAsync(entry, _cts.Token).ConfigureAwait(false);
            _ = Interlocked.Increment(ref _queued);
        }
        catch
        {
            _ = Interlocked.Increment(ref _entriesDroppedCount);
        }
    }

    /// <summary>Flush the current buffer to disk.</summary>
    public void Flush() => _fileWriter.Flush();

    /// <summary>Generate a diagnostic report of the provider's state.</summary>
    public string GenerateReport()
        => $"FileLoggerProvider [UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]"
         + Environment.NewLine + $"- USER: {Environment.UserName}"
         + Environment.NewLine + $"- Log File: {Path.Combine(Directories.LogsDirectory, this.Options.LogFileName)}"
         + Environment.NewLine + $"- Written:  {this.TotalEntriesWritten:N0}"
         + Environment.NewLine + $"- Dropped:  {this.EntriesDroppedCount:N0}"
         + Environment.NewLine + $"- Queue:    ~{this.QueuedEntryCount:N0}/{_maxQueueSize}";

    /// <summary>
    /// Generates a dictionary containing diagnostic information about the provider state.
    /// </summary>
    public IDictionary<string, object> GenerateReportData()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["User"] = Environment.UserName,
            ["LogFile"] = Path.Combine(Directories.LogsDirectory, this.Options.LogFileName),
            ["Written"] = this.TotalEntriesWritten,
            ["Dropped"] = this.EntriesDroppedCount,
            ["Queue"] = this.QueuedEntryCount,
            ["MaxQueueSize"] = _maxQueueSize,
            ["BatchSize"] = _batchSize,
            ["AdaptiveFlush"] = _adaptiveFlush,
            ["MaxBatchDelayMs"] = _maxBatchDelay.TotalMilliseconds,
            ["BlockWhenFull"] = _blockWhenFull,
            ["Disposed"] = _disposed
        };
    }

    #endregion APIs

    #region Private Methods

    private async Task CONSUME_LOOP_ASYNC(
        IWorkerContext ctx,
        CancellationToken ct)
    {
        List<LogEntry> batch = new(_batchSize);
        Stopwatch sw = Stopwatch.StartNew();
        TimeSpan currentDelay = _maxBatchDelay;

        try
        {
            while (await _reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (!_reader.TryRead(out LogEntry first))
                {
                    continue;
                }

                batch.Add(first);
                _ = Interlocked.Decrement(ref _queued);

                long batchStartTicks = sw.ElapsedTicks;

                while (batch.Count < _batchSize)
                {
                    long elapsedSinceStart = sw.ElapsedTicks - batchStartTicks;
                    if (elapsedSinceStart >= currentDelay.Ticks)
                    {
                        break;
                    }

                    if (_reader.TryRead(out LogEntry item))
                    {
                        batch.Add(item);
                        _ = Interlocked.Decrement(ref _queued);
                    }
                    else
                    {
                        await Task.Yield();
                        break;
                    }
                }

                _fileWriter.WriteBatch(batch, _formatter);

                int batchCount = batch.Count;
                _ = Interlocked.Add(ref _totalEntriesWritten, batchCount);
                ctx.Advance(batchCount, "File logs written");
                ctx.Beat();
                batch.Clear();

                if (_adaptiveFlush)
                {
                    currentDelay = batchCount >= _batchSize - 1
                        ? TimeSpan.FromMilliseconds(
                            Math.Max(1, currentDelay.TotalMilliseconds * 0.75))
                        : TimeSpan.FromMilliseconds(
                            Math.Min(5000, currentDelay.TotalMilliseconds * 1.25));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LG.FileLogger] Consumer error: {ex}");
        }
        finally
        {
            while (_reader.TryRead(out LogEntry msg))
            {
                batch.Add(msg);

                if (batch.Count >= _batchSize)
                {
                    _fileWriter.WriteBatch(batch, _formatter);
                    int count = batch.Count;
                    _ = Interlocked.Add(ref _totalEntriesWritten, count);
                    ctx.Advance(count, "File logs written (shutdown)");
                    ctx.Beat();
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                _fileWriter.WriteBatch(batch, _formatter);
                int count = batch.Count;
                _ = Interlocked.Add(ref _totalEntriesWritten, count);
                ctx.Advance(count, "File logs written (shutdown)");
                ctx.Beat();
                batch.Clear();
            }
        }
    }

    #endregion Private Methods

    #region Dispose

    /// <summary>
    /// Releases all resources used by the provider.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _ = _writer.TryComplete();
            _cts.Cancel();

            if (_workerHandle != null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                               .CancelWorker(_workerHandle.Id);
            }

            _fileWriter.Flush();
            _fileWriter.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LG.FileLogger] Dispose error: {ex}");
        }
        finally
        {
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    #endregion Dispose
}
