// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Channels;
using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Logging.Configuration;
using Nalix.Logging.Internal.Formatters;
using Nalix.Logging.Internal.Pooling;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.Console;

/// <summary>
/// High-throughput channel console logger backend.
/// </summary>
internal sealed class ConsoleLoggerProvider : System.IDisposable
{
    #region Fields

    private readonly Channel<LogEntry> _channel;
    private readonly ChannelWriter<LogEntry> _writer;
    private readonly ChannelReader<LogEntry> _reader;

    private readonly LogFormatter _formatter;
    private readonly int _batchSize;
    private readonly bool _enableFlush;
    private readonly IWorkerHandle? _workerHandle;
    private readonly bool _enableColors;
    private readonly bool _adaptiveFlush;
    private readonly System.Threading.CancellationTokenSource _cts;

    private long _writtenCount;
    private long _droppedCount;
    private System.TimeSpan _batchDelay;

    private volatile bool _disposed;

    #endregion Fields

    #region Properties

    public long WrittenCount => System.Threading.Interlocked.Read(ref _writtenCount);
    public long DroppedCount => System.Threading.Interlocked.Read(ref _droppedCount);

    #endregion Properties

    #region Constructors

    public ConsoleLoggerProvider(ConsoleLogOptions? options = null)
    {
        options ??= ConfigurationManager.Instance.Get<ConsoleLogOptions>();

        _enableFlush = options.EnableFlush;
        _enableColors = options.EnableColors;
        _adaptiveFlush = options.AdaptiveFlush;
        _batchSize = System.Math.Max(1, options.BatchSize);
        _batchDelay = options.BatchDelay > System.TimeSpan.Zero ? options.BatchDelay : System.TimeSpan.FromMilliseconds(100);

        _formatter = new LogFormatter(_enableColors);

        ChannelOptions channelOptions = options.MaxQueueSize > 0
            ? new System.Threading.Channels.BoundedChannelOptions(options.MaxQueueSize)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = options.BlockWhenQueueFull ? System.Threading.Channels.BoundedChannelFullMode.Wait : System.Threading.Channels.BoundedChannelFullMode.DropWrite
            }
            : new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };

        _channel = options.MaxQueueSize > 0
            ? Channel.CreateBounded<LogEntry>((BoundedChannelOptions)channelOptions)
            : Channel.CreateUnbounded<LogEntry>((UnboundedChannelOptions)channelOptions);

        _writer = _channel.Writer;
        _reader = _channel.Reader;
        _cts = new System.Threading.CancellationTokenSource();

        _workerHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: "log.console.worker",
            group: "log",
            work: async (ctx, ct) =>
            {
                System.Threading.CancellationTokenSource linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                await CONSUME_LOOP_ASYNC(ctx, linkedCts.Token);
            },
            options: new WorkerOptions
            {
                Tag = "console-consumer",       // Gắn tag để report hoặc log
                GroupConcurrencyLimit = ConfigurationManager.Instance.Get<NLogixOptions>().GroupConcurrencyLimit,      // Chỉ chạy duy nhất 1 log worker cho nhóm này
                OnFailed = (st, ex) => System.Diagnostics.Debug.WriteLine($"[LG.WebhookLogger] Worker failed: {st.Name}, {ex.Message}"),
                OnCompleted = st => System.Diagnostics.Debug.WriteLine($"[LG.WebhookLogger] Worker completed: {st.Name} Runs={st.TotalRuns}"),
            });
    }

    #endregion Constructors

    #region API

    public bool TryEnqueue(LogEntry log)
    {
        if (_disposed)
        {
            return false;
        }

        if (_channel.Writer.TryWrite(log))
        {
            return true;
        }

        _ = System.Threading.Interlocked.Increment(ref _droppedCount);
        return false;
    }

    public async System.Threading.Tasks.ValueTask WriteAsync(LogEntry log)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await _writer.WriteAsync(log, _cts.Token).ConfigureAwait(false);
        }
        catch
        {
            _ = System.Threading.Interlocked.Increment(ref _droppedCount);
        }
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
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelWorker(_workerHandle.Id);
        }

        _cts.Dispose();
    }

    #endregion API

    #region Private Methods

    private async System.Threading.Tasks.Task CONSUME_LOOP_ASYNC(IWorkerContext ctx, System.Threading.CancellationToken ct)
    {
        System.Collections.Generic.List<LogEntry> batch = new(_batchSize);
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            while (await _reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                // always fetch at least 1
                if (!_reader.TryRead(out LogEntry first))
                {
                    continue;
                }

                batch.Add(first);

                long batchStartTicks = sw.ElapsedTicks;

                // accumulate batch
                while (batch.Count < _batchSize)
                {
                    if (sw.Elapsed - System.TimeSpan.FromTicks(batchStartTicks) >= _batchDelay)
                    {
                        break;
                    }

                    if (_reader.TryRead(out LogEntry log))
                    {
                        batch.Add(log);
                    }
                    else
                    {
                        await System.Threading.Tasks.Task.Yield();
                        break;
                    }
                }

                WRITE_BATCH(batch);
                int writtenInBatch = batch.Count;

                // **Update progress & heartbeat**
                ctx.Beat();
                ctx.Advance(batch.Count, "Logs written");

                batch.Clear();

                // adaptive flush
                if (_adaptiveFlush)
                {
                    if (writtenInBatch >= _batchSize - 1)
                    {
                        _batchDelay = System.TimeSpan.FromMilliseconds(System.Math.Max(1, _batchDelay.TotalMilliseconds * 0.75));
                    }
                    else if (writtenInBatch <= 2)
                    {
                        _batchDelay = System.TimeSpan.FromMilliseconds(System.Math.Min(2000, _batchDelay.TotalMilliseconds * 1.33));
                    }
                }
            }
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            while (_reader.TryRead(out LogEntry log))
            {
                batch.Add(log);
                if (batch.Count >= _batchSize)
                {
                    WRITE_BATCH(batch);
                    ctx.Advance(batch.Count, "Logs written (shutdown)");
                    ctx.Beat();
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                WRITE_BATCH(batch);
                ctx.Advance(batch.Count, "Logs written (shutdown)");
                ctx.Beat();
            }
        }
    }

    private void WRITE_BATCH(System.Collections.Generic.List<LogEntry> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        System.Text.StringBuilder sb = StringBuilderPool.Rent(capacity: batch.Count * 128);
        try
        {
            foreach (LogEntry entry in batch)
            {
                LogMessageBuilder.AppendFormatted(
                    sb, entry.TimeStamp, entry.LogLevel,
                    entry.EventId, entry.Message, entry.Exception, _enableColors);
                _ = sb.AppendLine();
            }

            System.Console.Out.Write(sb);
            _ = System.Threading.Interlocked.Add(ref _writtenCount, batch.Count);
        }
        catch
        {
            _ = System.Threading.Interlocked.Add(ref _droppedCount, batch.Count);
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

    #endregion Private Methods
}
