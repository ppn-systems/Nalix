// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Logging.Internal.Formatters;
using Nalix.Logging.Options;

namespace Nalix.Logging.Internal.Console;

/// <summary>
/// High-throughput channel console logger backend.
/// </summary>
internal sealed class ConsoleLoggerProvider : System.IDisposable
{
    #region Fields

    private readonly System.Threading.Channels.Channel<LogEntry> _channel;
    private readonly System.Threading.Channels.ChannelWriter<LogEntry> _writer;
    private readonly System.Threading.Channels.ChannelReader<LogEntry> _reader;

    private readonly LogFormatter _formatter;
    private readonly System.Int32 _batchSize;
    private readonly IWorkerHandle? _workerHandle;
    private readonly System.Boolean _enableColors;
    private readonly System.Boolean _adaptiveFlush;
    private readonly System.Threading.CancellationTokenSource _cts;

    private System.Int64 _writtenCount;
    private System.Int64 _droppedCount;
    private System.TimeSpan _batchDelay;

    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Properties

    public System.Int64 WrittenCount => System.Threading.Interlocked.Read(ref _writtenCount);
    public System.Int64 DroppedCount => System.Threading.Interlocked.Read(ref _droppedCount);

    #endregion Properties

    #region Constructors

    public ConsoleLoggerProvider(ConsoleLogOptions? options = null)
    {
        options ??= ConfigurationManager.Instance.Get<ConsoleLogOptions>();

        _enableColors = options.EnableColors;
        _adaptiveFlush = options.AdaptiveFlush;
        _batchSize = System.Math.Max(1, options.BatchSize);
        _batchDelay = options.BatchDelay > System.TimeSpan.Zero ? options.BatchDelay : System.TimeSpan.FromMilliseconds(100);

        _formatter = new LogFormatter(_enableColors);

        var channelOptions = options.MaxQueueSize > 0
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
            } as System.Threading.Channels.ChannelOptions;

        _channel = options.MaxQueueSize > 0
            ? System.Threading.Channels.Channel.CreateBounded<LogEntry>((System.Threading.Channels.BoundedChannelOptions)channelOptions)
            : System.Threading.Channels.Channel.CreateUnbounded<LogEntry>((System.Threading.Channels.UnboundedChannelOptions)channelOptions);

        _writer = _channel.Writer;
        _reader = _channel.Reader;
        _cts = new System.Threading.CancellationTokenSource();

        _workerHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: "log.console.worker",
            group: "log",
            work: async (ctx, ct) =>
            {
                System.Threading.CancellationTokenSource linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                await this.CONSUME_LOOP_ASYNC(ctx, linkedCts.Token);
            },
            options: new WorkerOptions
            {
                Tag = "console-consumer",       // Gắn tag để report hoặc log
                GroupConcurrencyLimit = 1,      // Chỉ chạy duy nhất 1 log worker cho nhóm này
                OnFailed = (st, ex) => System.Diagnostics.Debug.WriteLine($"[LG.WebhookLogger] Worker failed: {st.Name}, {ex.Message}"),
                OnCompleted = st => System.Diagnostics.Debug.WriteLine($"[LG.WebhookLogger] Worker completed: {st.Name} Runs={st.TotalRuns}"),
            });
    }

    #endregion Constructors

    #region API

    public System.Boolean TryEnqueue(LogEntry log)
    {
        if (_disposed)
        {
            return false;
        }

        if (_channel.Writer.TryWrite(log))
        {
            return true;
        }

        System.Threading.Interlocked.Increment(ref _droppedCount);
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
            System.Threading.Interlocked.Increment(ref _droppedCount);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writer.TryComplete();
        _cts.Cancel();

        if (_workerHandle != null)
        {
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
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
                if (!_reader.TryRead(out var first))
                {
                    continue;
                }

                batch.Add(first);

                System.Int64 batchStartTicks = sw.ElapsedTicks;

                // accumulate batch
                while (batch.Count < _batchSize)
                {
                    if (sw.Elapsed - System.TimeSpan.FromTicks(batchStartTicks) >= _batchDelay)
                    {
                        break;
                    }

                    if (_reader.TryRead(out var log))
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

                // **Update progress & heartbeat**
                ctx.Advance(batch.Count, "Logs written");
                ctx.Beat();

                batch.Clear();

                // adaptive flush
                if (_adaptiveFlush)
                {
                    if (batch.Count >= _batchSize - 1)
                    {
                        _batchDelay = System.TimeSpan.FromMilliseconds(System.Math.Max(1, _batchDelay.TotalMilliseconds * 0.75));
                    }
                    else if (batch.Count <= 2)
                    {
                        _batchDelay = System.TimeSpan.FromMilliseconds(System.Math.Min(2000, System.Math.Max(1, _batchDelay.TotalMilliseconds * 1.33)));
                    }
                }
            }
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            while (_reader.TryRead(out var log))
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
        foreach (var entry in batch)
        {
            try
            {
                System.String formatted = _formatter.Format(entry);
                System.Console.WriteLine(formatted);
                System.Threading.Interlocked.Increment(ref _writtenCount);
            }
            catch
            {
                System.Threading.Interlocked.Increment(ref _droppedCount);
            }
        }
    }

    #endregion Private Methods
}