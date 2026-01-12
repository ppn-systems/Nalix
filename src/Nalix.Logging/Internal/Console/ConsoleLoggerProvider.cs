// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
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

    private readonly System.Threading.Tasks.Task _consumerTask;
    private readonly System.Threading.CancellationTokenSource _cts;
    private readonly LogFormatter _formatter;
    private readonly System.Boolean _enableColors;
    private readonly System.Int32 _batchSize;
    private readonly System.Boolean _adaptiveFlush;
    private System.TimeSpan _batchDelay;

    private System.Int64 _writtenCount;
    private System.Int64 _droppedCount;
    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Properties

    public System.Int64 WrittenCount => System.Threading.Interlocked.Read(ref _writtenCount);
    public System.Int64 DroppedCount => System.Threading.Interlocked.Read(ref _droppedCount);

    #endregion Properties

    #region Constructors

    public ConsoleLoggerProvider(ConsoleLogOptions options)
    {
        _formatter = new LogFormatter(_enableColors);
        _batchSize = System.Math.Max(1, options.BatchSize);
        _enableColors = options.EnableColors;
        _adaptiveFlush = options.AdaptiveFlush;
        _batchDelay = options.BatchDelay > System.TimeSpan.Zero ? options.BatchDelay : System.TimeSpan.FromMilliseconds(100);

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

        _consumerTask = System.Threading.Tasks.Task.Run(ConsumeLoopAsync);
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
        try
        {
            _consumerTask.Wait(System.TimeSpan.FromSeconds(3));
        }
        catch { }
        _cts.Dispose();
    }

    #endregion API

    #region Private Methods

    private async System.Threading.Tasks.Task ConsumeLoopAsync()
    {
        System.Collections.Generic.List<LogEntry> batch = new(_batchSize);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            while (await _reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
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
                        // no data, yield a bit for latency
                        await System.Threading.Tasks.Task.Yield();
                        break;
                    }
                }

                WriteBatch(batch);
                batch.Clear();

                // adaptive flush
                if (_adaptiveFlush)
                {
                    if (batch.Count >= _batchSize - 1)
                    {
                        _batchDelay = System.TimeSpan.FromMilliseconds(System.Math.Max(1, _batchDelay.TotalMilliseconds * 0.75));
                    }
                    else if (batch.Count <= 2) // tiny batch? up delay
                    {
                        _batchDelay = System.TimeSpan.FromMilliseconds(System.Math.Min(2000, System.Math.Max(1, _batchDelay.TotalMilliseconds * 1.33)));
                    }
                }
            }
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            // drain remaining logs
            while (_reader.TryRead(out var log))
            {
                batch.Add(log);
                if (batch.Count >= _batchSize)
                {
                    WriteBatch(batch);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                WriteBatch(batch);
            }
        }
    }

    private void WriteBatch(System.Collections.Generic.List<LogEntry> batch)
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