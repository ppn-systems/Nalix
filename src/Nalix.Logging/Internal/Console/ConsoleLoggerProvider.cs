// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Concurrency;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Logging.Internal.Pooling;
using Nalix.Logging.Options;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.Console;

/// <summary>
/// High-throughput channel console logger backend.
/// </summary>
internal sealed class ConsoleLoggerProvider : IDisposable
{
    private readonly Channel<LogMessage> _channel;
    private readonly INLogixFormatter _formatter;
    private readonly ChannelWriter<LogMessage> _writer;
    private readonly ChannelReader<LogMessage> _reader;
    private readonly int _batchSize;
    private readonly bool _enableFlush;
    private readonly IWorkerHandle? _workerHandle;
    private readonly bool _adaptiveFlush;
    private readonly CancellationTokenSource _cts;
    private long _writtenCount;
    private long _droppedCount;
    private TimeSpan _batchDelay;
    private volatile bool _disposed;

    public long WrittenCount => Interlocked.Read(ref _writtenCount);
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    public ConsoleLoggerProvider(INLogixFormatter formatter, ConsoleLogOptions? options = null)
    {
        options ??= ConfigurationManager.Instance.Get<ConsoleLogOptions>();

        _formatter = formatter;
        _enableFlush = options.EnableFlush;
        _adaptiveFlush = options.AdaptiveFlush;
        _batchSize = Math.Max(1, options.BatchSize);
        _batchDelay = options.BatchDelay > TimeSpan.Zero ? options.BatchDelay : TimeSpan.FromMilliseconds(100);

        ChannelOptions channelOptions = options.MaxQueueSize > 0
            ? new BoundedChannelOptions(options.MaxQueueSize)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = options.BlockWhenQueueFull ? BoundedChannelFullMode.Wait : BoundedChannelFullMode.DropWrite
            }
            : new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };

        _channel = options.MaxQueueSize > 0
            ? Channel.CreateBounded<LogMessage>((BoundedChannelOptions)channelOptions)
            : Channel.CreateUnbounded<LogMessage>((UnboundedChannelOptions)channelOptions);

        _writer = _channel.Writer;
        _reader = _channel.Reader;
        _cts = new CancellationTokenSource();

        _workerHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: "log.console.worker",
            group: "log",
            work: async (ctx, ct) =>
            {
                using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                await this.CONSUME_LOOP_ASYNC(ctx, linkedCts.Token).ConfigureAwait(false);
            },
            options: new WorkerOptions
            {
                Tag = "console-consumer",
                GroupConcurrencyLimit = ConfigurationManager.Instance.Get<NLogixOptions>().GroupConcurrencyLimit,
                OnFailed = (st, ex) => Debug.WriteLine($"[LG.WebhookLogger] Worker failed: {st.Name}, {ex.Message}"),
                OnCompleted = st => Debug.WriteLine($"[LG.WebhookLogger] Worker completed: {st.Name} Runs={st.TotalRuns}"),
            });
    }

    public bool TryEnqueue(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (_disposed)
        {
            return false;
        }

        if (_channel.Writer.TryWrite(new LogMessage(timestampUtc, logLevel, eventId, message, exception)))
        {
            return true;
        }

        _ = Interlocked.Increment(ref _droppedCount);
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = _writer.TryComplete();
        _cts.Cancel();

        if (_workerHandle != null)
        {
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelWorker(_workerHandle.Id);
        }

        _cts.Dispose();
    }

    private async Task CONSUME_LOOP_ASYNC(IWorkerContext ctx, CancellationToken ct)
    {
        List<LogMessage> batch = new(_batchSize);
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            while (await _reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (!_reader.TryRead(out LogMessage first))
                {
                    continue;
                }

                batch.Add(first);

                long batchStartTicks = sw.ElapsedTicks;
                long maxBatchDelayStopwatchTicks = _batchDelay.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;

                while (batch.Count < _batchSize)
                {
                    if (sw.ElapsedTicks - batchStartTicks >= maxBatchDelayStopwatchTicks)
                    {
                        break;
                    }

                    if (_reader.TryRead(out LogMessage log))
                    {
                        batch.Add(log);
                    }
                    else
                    {
                        await Task.Yield();
                        break;
                    }
                }

                this.WRITE_BATCH(batch);
                int writtenInBatch = batch.Count;

                ctx.Beat();
                ctx.Advance(batch.Count, "Logs written");
                batch.Clear();

                if (_adaptiveFlush)
                {
                    if (writtenInBatch >= _batchSize - 1)
                    {
                        _batchDelay = TimeSpan.FromMilliseconds(Math.Max(1, _batchDelay.TotalMilliseconds * 0.75));
                    }
                    else if (writtenInBatch <= 2)
                    {
                        _batchDelay = TimeSpan.FromMilliseconds(Math.Min(2000, _batchDelay.TotalMilliseconds * 1.33));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            while (_reader.TryRead(out LogMessage log))
            {
                batch.Add(log);
                if (batch.Count >= _batchSize)
                {
                    this.WRITE_BATCH(batch);
                    ctx.Advance(batch.Count, "Logs written (shutdown)");
                    ctx.Beat();
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                this.WRITE_BATCH(batch);
                ctx.Advance(batch.Count, "Logs written (shutdown)");
                ctx.Beat();
            }
        }
    }

    private void WRITE_BATCH(List<LogMessage> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        StringBuilder sb = StringBuilderPool.Rent(capacity: batch.Count * 128);
        try
        {
            foreach (LogMessage entry in batch)
            {
                _formatter.Format(entry.TimestampUtc, entry.LogLevel, entry.EventId, entry.Message, entry.Exception, sb);
                _ = sb.AppendLine();
            }

            System.Console.Write(sb);
            _ = Interlocked.Add(ref _writtenCount, batch.Count);
        }
        catch
        {
            _ = Interlocked.Add(ref _droppedCount, batch.Count);
        }
        finally
        {
            StringBuilderPool.Return(sb);
            if (_enableFlush)
            {
                System.Console.Out.Flush();
            }
        }
    }

    internal readonly record struct LogMessage(
        DateTime TimestampUtc,
        LogLevel LogLevel,
        EventId EventId,
        string Message,
        Exception? Exception);
}
