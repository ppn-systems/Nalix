// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Environment;
using Nalix.Common.Shared.Abstractions;
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
/// Optimized for low contention and fewer syscalls.
/// </summary>
/// <remarks>
/// - Channel lưu <see cref="LogEntry"/> thô — formatting xảy ra tập trung tại consumer thread,
///   tránh contention trên <c>StringBuilderPool</c> ở phía producer.
/// - Single consumer background task reads từ bounded channel.
/// - Producers dùng TryWrite (drop) hoặc WriteAsync (block) tùy theo
///   <see cref="FileLogOptions.BlockWhenQueueFull"/>.
/// - Batching: flush theo item count hoặc elapsed time, tùy cái nào đến trước.
/// - Adaptive flush interval có thể bật/tắt qua constructor.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("Queued={QueuedEntryCount}, Written={TotalEntriesWritten}, Dropped={EntriesDroppedCount}")]
internal sealed class FileLoggerProvider : System.IDisposable, IReportable
{
    #region Fields

    // ✅ Channel giờ lưu LogEntry thô thay vì string đã format
    // → Producer không cần format → không contention trên StringBuilderPool
    private readonly System.Threading.Channels.Channel<LogEntry> _channel;
    private readonly System.Threading.Channels.ChannelWriter<LogEntry> _writer;
    private readonly System.Threading.Channels.ChannelReader<LogEntry> _reader;

    private readonly FileWriter _fileWriter;
    private readonly IWorkerHandle? _workerHandle;
    private readonly System.Threading.CancellationTokenSource _cts;

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
    /// Khởi tạo <see cref="FileLoggerProvider"/>.
    /// </summary>
    /// <param name="formatter">Formatter dùng để chuyển <see cref="LogEntry"/> thành string.</param>
    /// <param name="options">Tùy chọn cấu hình file logger.</param>
    /// <param name="batchSize">Số entry tối đa mỗi batch trước khi flush (mặc định: 256).</param>
    /// <param name="maxBatchDelay">Thời gian tối đa chờ trước khi flush batch chưa đầy (mặc định: options.FlushInterval hoặc 1s).</param>
    /// <param name="adaptiveFlush">Bật adaptive flush dựa trên tốc độ incoming (mặc định: true).</param>
    public FileLoggerProvider(
        ILoggerFormatter formatter,
        FileLogOptions? options = null,
        System.Int32 batchSize = 256,
        System.TimeSpan? maxBatchDelay = null,
        System.Boolean adaptiveFlush = true)
    {
        Options = options ?? ConfigurationManager.Instance.Get<FileLogOptions>();

        _cts = new System.Threading.CancellationTokenSource();
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

        // ✅ Channel<LogEntry>: bounded, single reader, many writers
        System.Threading.Channels.BoundedChannelOptions channelOptions = new(_maxQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = _blockWhenFull
                ? System.Threading.Channels.BoundedChannelFullMode.Wait
                : System.Threading.Channels.BoundedChannelFullMode.DropNewest
        };

        _channel = System.Threading.Channels.Channel.CreateBounded<LogEntry>(channelOptions);
        _writer = _channel.Writer;
        _reader = _channel.Reader;

        _fileWriter = new FileWriter(this);

        _workerHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
            .ScheduleWorker(
                name: "log.file.worker",
                group: "log",
                work: async (ctx, ct) =>
                {
                    using System.Threading.CancellationTokenSource linkedCts =
                        System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                    await CONSUME_LOOP_ASYNC(ctx, linkedCts.Token).ConfigureAwait(false);
                },
                options: new WorkerOptions
                {
                    Tag = "file-consumer",
                    GroupConcurrencyLimit = ConfigurationManager.Instance.Get<NLogixOptions>().GroupConcurrencyLimit,
                    OnFailed = (st, ex) => System.Diagnostics.Debug.WriteLine(
                        $"[LG.FileLogger] Worker failed: {st.Name}, {ex.Message}"),
                    OnCompleted = st => System.Diagnostics.Debug.WriteLine(
                        $"[LG.FileLogger] Worker completed: {st.Name} Runs={st.TotalRuns}"),
                }
            );
    }

    #endregion Constructors

    #region Properties

    /// <summary>Tùy chọn cấu hình đang dùng.</summary>
    public FileLogOptions Options { get; }

    /// <summary>Số entry đang chờ trong queue (ước lượng).</summary>
    public System.Int32 QueuedEntryCount
        => System.Math.Max(0, System.Threading.Volatile.Read(ref _queued));

    /// <summary>Tổng số entry đã ghi thành công (từ lúc khởi động).</summary>
    public System.Int64 TotalEntriesWritten
        => System.Threading.Interlocked.Read(ref _totalEntriesWritten);

    /// <summary>Số entry bị drop do queue đầy.</summary>
    public System.Int64 EntriesDroppedCount
        => System.Threading.Interlocked.Read(ref _entriesDroppedCount);

    #endregion Properties

    #region APIs

    /// <summary>
    /// Enqueue một <see cref="LogEntry"/> để ghi vào file.
    /// Không format ở đây — formatting xảy ra tại consumer thread.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void Enqueue(LogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        // ✅ TryWrite trực tiếp LogEntry — không format, không allocation ở đây
        if (_writer.TryWrite(entry))
        {
            System.Threading.Interlocked.Increment(ref _queued);
        }
        else
        {
            System.Threading.Interlocked.Increment(ref _entriesDroppedCount);
        }
    }

    /// <summary>
    /// Enqueue bất đồng bộ — dùng khi <see cref="FileLogOptions.BlockWhenQueueFull"/> = true.
    /// </summary>
    internal async System.Threading.Tasks.ValueTask EnqueueAsync(LogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // ✅ Await đúng cách — thực sự block khi queue đầy
            await _writer.WriteAsync(entry, _cts.Token).ConfigureAwait(false);
            System.Threading.Interlocked.Increment(ref _queued);
        }
        catch
        {
            System.Threading.Interlocked.Increment(ref _entriesDroppedCount);
        }
    }

    /// <summary>Flush buffer hiện tại xuống disk.</summary>
    public void Flush() => _fileWriter.Flush();

    /// <summary>Thông tin chẩn đoán về trạng thái provider.</summary>
    public System.String GenerateReport()
        => $"FileLoggerProvider [UTC: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]"
         + System.Environment.NewLine + $"- USER: {System.Environment.UserName}"
         + System.Environment.NewLine + $"- Log File: {System.IO.Path.Combine(Directories.LogsDirectory, Options.LogFileName)}"
         + System.Environment.NewLine + $"- Written:  {TotalEntriesWritten:N0}"
         + System.Environment.NewLine + $"- Dropped:  {EntriesDroppedCount:N0}"
         + System.Environment.NewLine + $"- Queue:    ~{QueuedEntryCount:N0}/{_maxQueueSize}";

    #endregion APIs

    #region Private Methods

    private async System.Threading.Tasks.Task CONSUME_LOOP_ASYNC(
        IWorkerContext ctx,
        System.Threading.CancellationToken ct)
    {
        // ✅ Batch giờ là List<LogEntry> thô — format tập trung tại FileWriter
        System.Collections.Generic.List<LogEntry> batch = new(_batchSize);
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        System.TimeSpan currentDelay = _maxBatchDelay;

        try
        {
            while (await _reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                // Đọc entry đầu tiên
                if (!_reader.TryRead(out LogEntry first))
                {
                    continue;
                }

                batch.Add(first);
                System.Threading.Interlocked.Decrement(ref _queued);

                // Gom thêm entries trong khoảng thời gian currentDelay
                System.Int64 batchStartTicks = sw.ElapsedTicks;

                while (batch.Count < _batchSize)
                {
                    // Kiểm tra timeout bằng ticks — tránh tạo TimeSpan object mỗi vòng lặp
                    System.Int64 elapsedSinceStart = sw.ElapsedTicks - batchStartTicks;
                    if (elapsedSinceStart >= currentDelay.Ticks)
                    {
                        break;
                    }

                    if (_reader.TryRead(out LogEntry item))
                    {
                        batch.Add(item);
                        System.Threading.Interlocked.Decrement(ref _queued);
                    }
                    else
                    {
                        await System.Threading.Tasks.Task.Yield();
                        break;
                    }
                }

                // ✅ FileWriter nhận LogEntry list, format + write trong 1 pass
                _fileWriter.WriteBatch(batch, _formatter);

                // ✅ Capture batchCount TRƯỚC khi Clear — fix bug adaptive flush
                System.Int32 batchCount = batch.Count;
                System.Threading.Interlocked.Add(ref _totalEntriesWritten, batchCount);
                ctx.Advance(batchCount, "File logs written");
                ctx.Beat();
                batch.Clear();

                // Adaptive flush: điều chỉnh delay dựa trên mức độ bận
                if (_adaptiveFlush)
                {
                    currentDelay = batchCount >= _batchSize - 1
                        // Batch đầy → đang bận → giảm delay để flush nhanh hơn
                        ? System.TimeSpan.FromMilliseconds(
                            System.Math.Max(1, currentDelay.TotalMilliseconds * 0.75))
                        // Batch thưa → đang nhàn → tăng delay để giảm CPU
                        : System.TimeSpan.FromMilliseconds(
                            System.Math.Min(5000, currentDelay.TotalMilliseconds * 1.25));
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            // Thoát bình thường — draining bên dưới
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LG.FileLogger] Consumer error: {ex}");
        }
        finally
        {
            // Drain toàn bộ entries còn lại trước khi shutdown
            while (_reader.TryRead(out LogEntry msg))
            {
                batch.Add(msg);

                if (batch.Count >= _batchSize)
                {
                    _fileWriter.WriteBatch(batch, _formatter);
                    System.Int32 count = batch.Count;
                    System.Threading.Interlocked.Add(ref _totalEntriesWritten, count);
                    ctx.Advance(count, "File logs written (shutdown)");
                    ctx.Beat();
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                _fileWriter.WriteBatch(batch, _formatter);
                System.Int32 count = batch.Count;
                System.Threading.Interlocked.Add(ref _totalEntriesWritten, count);
                ctx.Advance(count, "File logs written (shutdown)");
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
            _writer.TryComplete();
            _cts.Cancel();

            if (_workerHandle != null)
            {
                InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                               .CancelWorker(_workerHandle.Id);
            }

            _fileWriter.Flush();
            _fileWriter.Dispose();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LG.FileLogger] Dispose error: {ex}");
        }
        finally
        {
            _cts.Dispose();
            System.GC.SuppressFinalize(this);
        }
    }

    #endregion Dispose
}