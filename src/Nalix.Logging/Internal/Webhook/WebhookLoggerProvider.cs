// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Logging.Configuration;

namespace Nalix.Logging.Internal.Webhook;

/// <summary>
/// High-throughput channel-based webhook logger backend.
/// Manages batching, queuing, and asynchronous delivery of logs to Discord webhook.
/// </summary>
internal sealed class WebhookLoggerProvider : System.IDisposable
{
    #region Fields

    private readonly System.Threading.Channels.Channel<LogEntry> _channel;
    private readonly System.Threading.Channels.ChannelWriter<LogEntry> _writer;
    private readonly System.Threading.Channels.ChannelReader<LogEntry> _reader;

    private readonly WebhookLogOptions _options;
    private readonly IWorkerHandle? _workerHandle;
    private readonly WebhookHttpClient _webhookClient;
    private readonly System.Threading.CancellationTokenSource _cts;

    private System.Int64 _failedCount;
    private System.Int64 _writtenCount;
    private System.Int64 _droppedCount;
    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the minimum log level to process.
    /// </summary>
    public LogLevel MinimumLevel => _options.MinimumLevel;

    /// <summary>
    /// Gets the total number of failed webhook calls after all retry attempts.
    /// </summary>
    public System.Int64 FailedCount => System.Threading.Interlocked.Read(ref _failedCount);

    /// <summary>
    /// Gets the total number of log entries successfully sent to Discord.
    /// </summary>
    public System.Int64 WrittenCount => System.Threading.Interlocked.Read(ref _writtenCount);

    /// <summary>
    /// Gets the total number of log entries that were dropped due to queue full.
    /// </summary>
    public System.Int64 DroppedCount => System.Threading.Interlocked.Read(ref _droppedCount);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookLoggerProvider"/> class.
    /// </summary>
    /// <param name="options">The webhook log options. </param>
    public WebhookLoggerProvider(WebhookLogOptions? options = null)
    {
        _options = options ?? ConfigurationManager.Instance.Get<WebhookLogOptions>();
        _webhookClient = new WebhookHttpClient(_options);

        System.Threading.Channels.ChannelOptions channelOptions = _options.MaxQueueSize > 0
            ? new System.Threading.Channels.BoundedChannelOptions(_options.MaxQueueSize)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = _options.BlockWhenQueueFull
                    ? System.Threading.Channels.BoundedChannelFullMode.Wait
                    : System.Threading.Channels.BoundedChannelFullMode.DropWrite
            }
            : new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };

        _channel = _options.MaxQueueSize > 0
            ? System.Threading.Channels.Channel.CreateBounded<LogEntry>(
                (System.Threading.Channels.BoundedChannelOptions)channelOptions)
            : System.Threading.Channels.Channel.CreateUnbounded<LogEntry>(
                (System.Threading.Channels.UnboundedChannelOptions)channelOptions);

        _writer = _channel.Writer;
        _reader = _channel.Reader;
        _cts = new System.Threading.CancellationTokenSource();

        _workerHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: "log.webhook.worker",
            group: "log",
            work: async (ctx, ct) =>
            {
                using var linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                await CONSUME_LOOP_ASYNC(ctx, linkedCts.Token);
            },
            options: new WorkerOptions
            {
                Tag = "webhook-consumer",
                GroupConcurrencyLimit = 1,
                OnFailed = (st, ex) => System.Diagnostics.Debug.WriteLine($"[LG.WebhookLogger] Worker failed: {st.Name}, {ex.Message}"),
                OnCompleted = st => System.Diagnostics.Debug.WriteLine($"[LG.WebhookLogger] Worker completed: {st.Name} Runs={st.TotalRuns}"),
            }
        );
    }

    #endregion Constructors

    #region API

    /// <summary>
    /// Attempts to enqueue a log entry to the channel.
    /// </summary>
    /// <param name="entry">The log entry to enqueue.</param>
    /// <returns>True if the entry was enqueued; otherwi..se, false.</returns>
    public System.Boolean TryEnqueue(LogEntry entry)
    {
        if (_disposed)
        {
            return false;
        }

        if (_writer.TryWrite(entry))
        {
            return true;
        }

        System.Threading.Interlocked.Increment(ref _droppedCount);
        return false;
    }

    /// <summary>
    /// Asynchronously writes a log entry to the channel.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    /// <returns>A <see cref="System.Threading. Tasks.ValueTask"/> representing the asynchronous operation.</returns>
    public async System.Threading.Tasks.ValueTask WriteAsync(LogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await _writer.WriteAsync(entry, _cts.Token).ConfigureAwait(false);
        }
        catch
        {
            System.Threading.Interlocked.Increment(ref _droppedCount);
        }
    }

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
        _writer.TryComplete();
        _cts.Cancel();

        if (_workerHandle != null)
        {
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelWorker(_workerHandle.Id);
        }

        _webhookClient.Dispose();
        _cts.Dispose();
    }

    #endregion API

    #region Private Methods

    /// <summary>
    /// The main consumer loop that reads from the channel and sends batches to Discord.
    /// </summary>
    private async System.Threading.Tasks.Task CONSUME_LOOP_ASYNC(IWorkerContext ctx, System.Threading.CancellationToken ct)
    {
        System.Collections.Generic.List<LogEntry> batch = new(_options.BatchSize);

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

                // Lấy thêm log đủ batch size
                while (batch.Count < _options.BatchSize && _reader.TryRead(out var log))
                {
                    batch.Add(log);
                }

                // Gửi batch lên webhook
                await DISPATCH_LOG_BATCH_ASYNC(batch, ct).ConfigureAwait(false);

                ctx.Advance(batch.Count, "Logs sent to webhook");
                ctx.Beat();

                batch.Clear();

                // Delay theo cấu hình để tránh rate limit
                if (_options.BatchDelay > System.TimeSpan.Zero)
                {
                    await System.Threading.Tasks.Task.Delay(_options.BatchDelay, ct).ConfigureAwait(false);
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            // Expected during shutdown
        }
        finally
        {
            while (_reader.TryRead(out var log))
            {
                batch.Add(log);
                if (batch.Count >= _options.BatchSize)
                {
                    await DISPATCH_LOG_BATCH_ASYNC(batch, ct).ConfigureAwait(false);
                    ctx.Advance(batch.Count, "Logs sent to webhook (shutdown)");
                    ctx.Beat();
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                await DISPATCH_LOG_BATCH_ASYNC(batch, ct).ConfigureAwait(false);
                ctx.Advance(batch.Count, "Logs sent to webhook (shutdown)");
                ctx.Beat();
            }
        }
    }


    /// <summary>
    /// Sends a batch of log entries to Discord webhook.
    /// </summary>
    /// <param name="batch">The batch of log entries to send. </param>
    /// <param name="ct"></param>
    private async System.Threading.Tasks.Task DISPATCH_LOG_BATCH_ASYNC(System.Collections.Generic.List<LogEntry> batch, System.Threading.CancellationToken ct)
    {
        if (batch.Count is 0)
        {
            return;
        }

        try
        {
            System.Boolean success = await _webhookClient.SendAsync(batch, ct).ConfigureAwait(false);

            if (success)
            {
                System.Threading.Interlocked.Add(ref _writtenCount, batch.Count);
            }
            else
            {
                System.Threading.Interlocked.Add(ref _failedCount, batch.Count);
                HANDLE_WEBHOOK_ERROR(null, $"Failed to send {batch.Count} log(s) to Discord after all retries.");
            }
        }
        catch (System.Exception ex)
        {
            System.Threading.Interlocked.Add(ref _failedCount, batch.Count);
            HANDLE_WEBHOOK_ERROR(ex, $"Exception sending {batch.Count} log(s) to Discord: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles webhook errors by invoking the custom error handler if configured.
    /// </summary>
    /// <param name="exception">The exception that occurred, if any.</param>
    /// <param name="message">The error message. </param>
    private void HANDLE_WEBHOOK_ERROR(System.Exception? exception, System.String message)
    {
        try
        {
            _options.OnWebhookError?.Invoke(
                exception ?? new System.InvalidOperationException(message),
                message);
        }
        catch
        {
            // Ignore errors in error handler
        }

        System.Diagnostics.Debug.WriteLine($"[LG.ChannelWebhookLoggerProvider] {message}");
    }

    #endregion Private Methods
}