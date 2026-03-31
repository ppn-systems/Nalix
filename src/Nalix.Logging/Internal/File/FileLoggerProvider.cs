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
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;
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
    private readonly Channel<LogMessage> _channel;
    private readonly ChannelWriter<LogMessage> _writer;
    private readonly ChannelReader<LogMessage> _reader;
    private readonly FileWriter _fileWriter;
    private readonly IWorkerHandle? _workerHandle;
    private readonly CancellationTokenSource _cts;
    private readonly int _maxQueueSize;
    private readonly bool _blockWhenFull;
    private readonly int _batchSize;
    private readonly INLogixFormatter _formatter;
    private readonly bool _adaptiveFlush;
    private readonly TimeSpan _maxBatchDelay;
    private int _queued;
    private bool _disposed;
    private long _totalEntriesWritten;
    private long _entriesDroppedCount;

    public FileLoggerProvider(
        INLogixFormatter formatter,
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
            FullMode = _blockWhenFull ? BoundedChannelFullMode.Wait : BoundedChannelFullMode.DropNewest
        };

        _channel = Channel.CreateBounded<LogMessage>(channelOptions);
        _writer = _channel.Writer;
        _reader = _channel.Reader;
        _fileWriter = new FileWriter(this);

        _workerHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
            .ScheduleWorker(
                name: "log.file.worker",
                group: "log",
                work: async (ctx, ct) =>
                {
                    using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                    await this.CONSUME_LOOP_ASYNC(ctx, linkedCts.Token).ConfigureAwait(false);
                },
                options: new WorkerOptions
                {
                    Tag = "file-consumer",
                    GroupConcurrencyLimit = ConfigurationManager.Instance.Get<NLogixOptions>().GroupConcurrencyLimit,
                    OnFailed = (st, ex) => Debug.WriteLine($"[LG.FileLogger] Worker failed: {st.Name}, {ex.Message}"),
                    OnCompleted = st => Debug.WriteLine($"[LG.FileLogger] Worker completed: {st.Name} Runs={st.TotalRuns}"),
                });
    }

    public FileLogOptions Options { get; }
    public int QueuedEntryCount => Math.Max(0, Volatile.Read(ref _queued));
    public long TotalEntriesWritten => Interlocked.Read(ref _totalEntriesWritten);
    public long EntriesDroppedCount => Interlocked.Read(ref _entriesDroppedCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void Enqueue(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (_disposed)
        {
            return;
        }

        _ = _writer.TryWrite(new LogMessage(timestampUtc, logLevel, eventId, message, exception))
            ? Interlocked.Increment(ref _queued)
            : Interlocked.Increment(ref _entriesDroppedCount);
    }

    internal async ValueTask EnqueueAsync(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await _writer.WriteAsync(new LogMessage(timestampUtc, logLevel, eventId, message, exception), _cts.Token).ConfigureAwait(false);
            _ = Interlocked.Increment(ref _queued);
        }
        catch
        {
            _ = Interlocked.Increment(ref _entriesDroppedCount);
        }
    }

    public void Flush() => _fileWriter.Flush();

    public string GenerateReport()
        => $"FileLoggerProvider [UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]"
         + Environment.NewLine + $"- USER: {Environment.UserName}"
         + Environment.NewLine + $"- Log File: {Path.Combine(Directories.LogsDirectory, this.Options.LogFileName)}"
         + Environment.NewLine + $"- Written:  {this.TotalEntriesWritten:N0}"
         + Environment.NewLine + $"- Dropped:  {this.EntriesDroppedCount:N0}"
         + Environment.NewLine + $"- Queue:    ~{this.QueuedEntryCount:N0}/{_maxQueueSize}";

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

    private async Task CONSUME_LOOP_ASYNC(IWorkerContext ctx, CancellationToken ct)
    {
        List<LogMessage> batch = new(_batchSize);
        Stopwatch sw = Stopwatch.StartNew();
        TimeSpan currentDelay = _maxBatchDelay;

        try
        {
            while (await _reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (!_reader.TryRead(out LogMessage first))
                {
                    continue;
                }

                batch.Add(first);
                _ = Interlocked.Decrement(ref _queued);

                long batchStartTicks = sw.ElapsedTicks;
                long maxBatchDelayStopwatchTicks = currentDelay.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;

                while (batch.Count < _batchSize)
                {
                    long elapsedSinceStart = sw.ElapsedTicks - batchStartTicks;
                    if (elapsedSinceStart >= maxBatchDelayStopwatchTicks)
                    {
                        break;
                    }

                    if (_reader.TryRead(out LogMessage item))
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
                        ? TimeSpan.FromMilliseconds(Math.Max(1, currentDelay.TotalMilliseconds * 0.75))
                        : TimeSpan.FromMilliseconds(Math.Min(5000, currentDelay.TotalMilliseconds * 1.25));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"[LG.FileLogger] Consumer error: {ex}");
#else
            GC.KeepAlive(ex);
#endif
        }
        finally
        {
            while (_reader.TryRead(out LogMessage msg))
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
                InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                        .CancelWorker(_workerHandle.Id);
            }

            _fileWriter.Flush();
            _fileWriter.Dispose();
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"[LG.FileLogger] Dispose error: {ex}");
#else
            GC.KeepAlive(ex);
#endif
        }
        finally
        {
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    internal readonly record struct LogMessage(
        DateTime TimestampUtc,
        LogLevel LogLevel,
        EventId EventId,
        string Message,
        Exception? Exception);
}
