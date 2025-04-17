using Notio.Common.Package.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Notio.Network.Dispatcher.Channel;

/// <summary>
/// Hàng đợi packet hiệu năng cao sử dụng System.Threading.Channels
/// để tối ưu hóa truyền dữ liệu giữa các thread, hỗ trợ ưu tiên đa cấp độ.
/// </summary>
internal sealed class PacketChannel<TPacket> where TPacket : Common.Package.IPacket
{
    // Kênh cho mỗi mức ưu tiên
    private readonly Channel<TPacket>[] _priorityChannels;
    private readonly int _priorityCount;

    // Theo dõi số lượng packet
    private int _totalCount;

    // Cài đặt
    private readonly int _maxQueueSize;
    private readonly TimeSpan _packetTimeout;
    private readonly bool _validateOnDequeue;

    // Theo dõi hiệu suất
    private readonly Stopwatch _queueTimer;
    private long _totalEnqueued;
    private long _totalDequeued;
    private long _totalExpired;
    private long _totalInvalid;

    // Sự kiện để thông báo khi có packet mới
    private readonly SemaphoreSlim _packetAvailableSemaphore;

    /// <summary>
    /// Khởi tạo hàng đợi packet mới sử dụng System.Threading.Channels
    /// </summary>
    /// <param name="maxQueueSize">Kích thước tối đa của queue (0 = không giới hạn)</param>
    /// <param name="packetTimeout">Thời gian tối đa một packet tồn tại trong queue</param>
    /// <param name="validateOnDequeue">Kiểm tra tính hợp lệ của packet khi lấy ra</param>
    /// <param name="boundedChannels">Sử dụng BoundedChannel thay vì UnboundedChannel</param>
    public PacketChannel(
        int maxQueueSize = 0,
        TimeSpan? packetTimeout = null,
        bool validateOnDequeue = true,
        bool boundedChannels = false)
    {
        _priorityCount = Enum.GetValues<PacketPriority>().Length;
        _priorityChannels = new Channel<TPacket>[_priorityCount];

        _maxQueueSize = maxQueueSize;
        _packetTimeout = packetTimeout ?? TimeSpan.FromSeconds(30);
        _validateOnDequeue = validateOnDequeue;

        // Khởi tạo channels cho mỗi mức ưu tiên
        for (int i = 0; i < _priorityCount; i++)
        {
            if (boundedChannels && _maxQueueSize > 0)
            {
                // Mỗi mức ưu tiên chia sẻ một phần của tổng kích thước queue
                int channelSize = _maxQueueSize / _priorityCount;
                var options = new BoundedChannelOptions(Math.Max(1, channelSize))
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false,
                    AllowSynchronousContinuations = true
                };

                _priorityChannels[i] = System.Threading.Channels.Channel.CreateBounded<TPacket>(options);
            }
            else
            {
                var options = new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false,
                    AllowSynchronousContinuations = true
                };
                _priorityChannels[i] = System.Threading.Channels.Channel.CreateUnbounded<TPacket>(options);
            }
        }

        _packetAvailableSemaphore = new SemaphoreSlim(0);
        _queueTimer = new Stopwatch();
        _queueTimer.Start();
    }

    /// <summary>
    /// Thêm một packet vào hàng đợi
    /// </summary>
    /// <param name="packet">Packet cần thêm</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Task hoàn thành khi packet đã được thêm vào</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<bool> EnqueueAsync(TPacket packet, CancellationToken cancellationToken = default)
    {
        if (packet == null)
            return false;

        int priorityIndex = (int)packet.Priority;

        // Kiểm tra kích thước tối đa
        if (_maxQueueSize > 0 && Interlocked.CompareExchange(ref _totalCount, 0, 0) >= _maxQueueSize)
        {
            return false;
        }

        try
        {
            // Ghi packet vào channel tương ứng
            await _priorityChannels[priorityIndex].Writer.WriteAsync(packet, cancellationToken);

            // Tăng số lượng packet và thông báo có packet mới
            Interlocked.Increment(ref _totalCount);
            Interlocked.Increment(ref _totalEnqueued);

            // Thông báo có packet mới
            try
            {
                _packetAvailableSemaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Bỏ qua nếu đã đạt giới hạn semaphore
            }

            return true;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Thêm packet vào hàng đợi đồng bộ (không đợi)
    /// </summary>
    /// <param name="packet">Packet cần thêm</param>
    /// <returns>True nếu thêm thành công</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(TPacket packet)
    {
        if (packet == null)
            return false;

        int priorityIndex = (int)packet.Priority;

        // Kiểm tra kích thước tối đa
        if (_maxQueueSize > 0 && Interlocked.CompareExchange(ref _totalCount, 0, 0) >= _maxQueueSize)
        {
            return false;
        }

        // Thử ghi packet vào channel tương ứng
        if (_priorityChannels[priorityIndex].Writer.TryWrite(packet))
        {
            // Tăng số lượng packet và thông báo có packet mới
            Interlocked.Increment(ref _totalCount);
            Interlocked.Increment(ref _totalEnqueued);

            try
            {
                _packetAvailableSemaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Bỏ qua nếu đã đạt giới hạn semaphore
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Lấy packet từ hàng đợi theo thứ tự ưu tiên
    /// </summary>
    /// <param name="cancellationToken">Token hủy</param>
    /// <returns>Packet tiếp theo cần xử lý</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<TPacket> DequeueAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            // Đợi có packet
            await _packetAvailableSemaphore.WaitAsync(cancellationToken);

            // Kiểm tra từ mức ưu tiên cao nhất xuống thấp nhất
            for (int i = _priorityCount - 1; i >= 0; i--)
            {
                var reader = _priorityChannels[i].Reader;

                if (reader.TryRead(out TPacket? packet))
                {
                    // Giảm tổng số packet
                    Interlocked.Decrement(ref _totalCount);

                    bool isValid = true;
                    bool isExpired = false;

                    // Kiểm tra hết hạn
                    if (_packetTimeout != TimeSpan.Zero)
                    {
                        isExpired = packet.IsExpired(_packetTimeout);
                        if (isExpired)
                        {
                            Interlocked.Increment(ref _totalExpired);
                        }
                    }

                    // Kiểm tra tính hợp lệ
                    if (_validateOnDequeue && !isExpired)
                    {
                        isValid = packet.IsValid();
                        if (!isValid)
                        {
                            Interlocked.Increment(ref _totalInvalid);
                        }
                    }

                    // Nếu packet hợp lệ và chưa hết hạn, trả về
                    if (!isExpired && isValid)
                    {
                        Interlocked.Increment(ref _totalDequeued);
                        return packet;
                    }

                    // Giải phóng packet không hợp lệ/hết hạn
                    packet.Dispose();

                    // Thêm lại một slot vào semaphore vì packet này bị bỏ qua
                    try
                    {
                        _packetAvailableSemaphore.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                        // Bỏ qua nếu đã đạt giới hạn semaphore
                    }

                    // Tiếp tục kiểm tra packet khác
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Thử lấy packet từ hàng đợi không đợi
    /// </summary>
    /// <param name="packet">Packet được lấy ra nếu có</param>
    /// <returns>True nếu lấy được packet, False nếu hàng đợi trống</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out TPacket? packet)
    {
        packet = default;

        // Kiểm tra từ mức ưu tiên cao nhất xuống thấp nhất
        for (int i = _priorityCount - 1; i >= 0; i--)
        {
            var reader = _priorityChannels[i].Reader;

            if (reader.TryRead(out TPacket? tempPacket))
            {
                // Giảm tổng số packet
                Interlocked.Decrement(ref _totalCount);

                bool isValid = true;
                bool isExpired = false;

                // Kiểm tra hết hạn
                if (_packetTimeout != TimeSpan.Zero)
                {
                    isExpired = tempPacket.IsExpired(_packetTimeout);
                    if (isExpired)
                    {
                        Interlocked.Increment(ref _totalExpired);
                    }
                }

                // Kiểm tra tính hợp lệ
                if (_validateOnDequeue && !isExpired)
                {
                    isValid = tempPacket.IsValid();
                    if (!isValid)
                    {
                        Interlocked.Increment(ref _totalInvalid);
                    }
                }

                // Nếu packet hợp lệ và chưa hết hạn, trả về
                if (!isExpired && isValid)
                {
                    packet = tempPacket;
                    Interlocked.Increment(ref _totalDequeued);
                    return true;
                }

                // Giải phóng packet không hợp lệ/hết hạn
                tempPacket.Dispose();

                // Kiểm tra xem có packet khác trong cùng queue không
                if (reader.TryRead(out tempPacket))
                {
                    // Đẩy lại packet vào reader (không hiệu quả nhưng vẫn thử)
                    _priorityChannels[i].Writer.TryWrite(tempPacket);
                    Interlocked.Increment(ref _totalCount);
                }

                // Đã xử lý packet này, nhưng không phù hợp để trả về
                continue;
            }
        }

        return false;
    }

    /// <summary>
    /// Đợi một packet từ hàng đợi với timeout
    /// </summary>
    /// <param name="timeout">Thời gian tối đa chờ đợi</param>
    /// <param name="cancellationToken"></param>
    /// <returns>True nếu lấy được packet, False nếu hết thời gian chờ</returns>
    public async Task<bool> WaitForPacketAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            // Đợi có packet
            await _packetAvailableSemaphore.WaitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Hết thời gian chờ
            return false;
        }
    }

    /// <summary>
    /// Lấy nhiều packet cùng lúc, theo thứ tự ưu tiên
    /// </summary>
    /// <param name="maxCount">Số lượng packet tối đa cần lấy</param>
    /// <param name="cancellationToken">Token hủy</param>
    /// <returns>Danh sách các packet hợp lệ</returns>
    public async Task<List<TPacket>> DequeueBatchAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var result = new List<TPacket>(Math.Min(maxCount, Count));
        int dequeued = 0;

        while (dequeued < maxCount)
        {
            // Kiểm tra xem còn packet không
            if (Count == 0)
            {
                try
                {
                    // Đợi có packet mới
                    await _packetAvailableSemaphore.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            // Thử lấy packet
            if (TryDequeue(out TPacket? packet))
            {
                result.Add(packet);
                dequeued++;
            }
            else
            {
                // Không còn packet phù hợp, thoát
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Bắt đầu xử lý packet liên tục trong một vòng lặp riêng biệt
    /// </summary>
    /// <param name="processor">Hàm xử lý packet</param>
    /// <param name="cancellationToken">Token hủy</param>
    /// <returns>Task chạy trong nền</returns>
    public Task StartProcessingAsync(Func<TPacket, Task> processor, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TPacket packet = await DequeueAsync(cancellationToken);

                    try
                    {
                        // Xử lý packet
                        await processor(packet);
                    }
                    catch (Exception processingEx)
                    {
                        // Xử lý lỗi khi xử lý packet (có thể log ở đây)
                        Console.WriteLine($"Error processing packet: {processingEx.Message}");
                    }
                    finally
                    {
                        // Luôn giải phóng packet khi đã xử lý xong
                        packet.Dispose();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Bị hủy, thoát khỏi vòng lặp
                    break;
                }
                catch (Exception ex)
                {
                    // Xử lý lỗi chung (có thể log ở đây)
                    Console.WriteLine($"Unexpected error in packet processing loop: {ex.Message}");

                    // Đợi một chút trước khi thử lại để tránh loop liên tục nếu có lỗi
                    await Task.Delay(100, cancellationToken);
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Bắt đầu xử lý packet theo batch
    /// </summary>
    /// <param name="batchProcessor">Hàm xử lý batch packet</param>
    /// <param name="batchSize">Kích thước batch</param>
    /// <param name="maxWaitTime">Thời gian chờ tối đa để tích lũy đủ batch</param>
    /// <param name="cancellationToken">Token hủy</param>
    /// <returns>Task chạy trong nền</returns>
    public Task StartBatchProcessingAsync(
        Func<List<TPacket>, Task> batchProcessor,
        int batchSize = 10,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        var waitTime = maxWaitTime ?? TimeSpan.FromMilliseconds(100);

        return Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Tạo một batch mới
                    List<TPacket> batch = new List<TPacket>(batchSize);

                    // Lấy packet đầu tiên (với timeout)
                    if (await WaitForPacketAsync(waitTime, cancellationToken))
                    {
                        if (TryDequeue(out TPacket packet))
                        {
                            batch.Add(packet);
                        }
                    }

                    // Thêm các packet khác nếu có sẵn (không đợi)
                    while (batch.Count < batchSize && TryDequeue(out TPacket nextPacket))
                    {
                        batch.Add(nextPacket);
                    }

                    // Nếu đã có ít nhất 1 packet, xử lý batch
                    if (batch.Count > 0)
                    {
                        try
                        {
                            await batchProcessor(batch);
                        }
                        finally
                        {
                            // Giải phóng tất cả packet trong batch
                            foreach (var p in batch)
                            {
                                p.Dispose();
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Bị hủy, thoát khỏi vòng lặp
                    break;
                }
                catch (Exception ex)
                {
                    // Xử lý lỗi chung (có thể log ở đây)
                    Console.WriteLine($"Unexpected error in batch processing loop: {ex.Message}");

                    // Đợi một chút trước khi thử lại
                    await Task.Delay(100, cancellationToken);
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Đóng tất cả channel và ngừng nhận packet mới
    /// </summary>
    public void Complete()
    {
        for (int i = 0; i < _priorityCount; i++)
        {
            _priorityChannels[i].Writer.Complete();
        }
    }

    /// <summary>
    /// Xóa toàn bộ packet trong queue
    /// </summary>
    /// <returns>Số lượng packet đã xóa</returns>
    public async Task<int> ClearAsync()
    {
        int removedCount = 0;

        for (int i = 0; i < _priorityCount; i++)
        {
            var reader = _priorityChannels[i].Reader;

            // Đọc và xóa tất cả packet từ channel
            while (await reader.WaitToReadAsync(CancellationToken.None))
            {
                while (reader.TryRead(out TPacket packet))
                {
                    packet.Dispose();
                    removedCount++;
                    Interlocked.Decrement(ref _totalCount);
                }
            }
        }

        return removedCount;
    }

    /// <summary>
    /// Lấy thông tin thống kê của hàng đợi
    /// </summary>
    public ChannelQueueStatistics GetStatistics()
    {
        var queueSizes = new Dictionary<PacketPriority, int>();

        // Lấy kích thước hiện tại của mỗi channel
        for (int i = 0; i < _priorityCount; i++)
        {
            int approxSize = 0;

            // Ước tính kích thước queue bằng cách đếm số packet trong channel
            while (_priorityChannels[i].Reader.TryPeek(out _))
            {
                approxSize++;
            }

            queueSizes[(PacketPriority)i] = approxSize;
        }

        return new ChannelQueueStatistics
        {
            TotalCount = Count,
            QueueSizeByPriority = queueSizes,
            TotalEnqueued = Interlocked.Read(ref _totalEnqueued),
            TotalDequeued = Interlocked.Read(ref _totalDequeued),
            TotalExpired = Interlocked.Read(ref _totalExpired),
            TotalInvalid = Interlocked.Read(ref _totalInvalid),
            UptimeSeconds = (int)_queueTimer.Elapsed.TotalSeconds
        };
    }

    /// <summary>
    /// Tổng số packet trong hàng đợi
    /// </summary>
    public int Count => Interlocked.CompareExchange(ref _totalCount, 0, 0);

    /// <summary>
    /// Giải phóng tài nguyên khi bị hủy
    /// </summary>
    public void Dispose()
    {
        // Đóng tất cả channel
        Complete();

        // Cố gắng xóa tất cả packet
        try
        {
            ClearAsync().Wait();
        }
        catch
        {
            // Bỏ qua lỗi khi clear
        }

        // Giải phóng semaphore
        _packetAvailableSemaphore.Dispose();
    }
}

/// <summary>
/// Thống kê về PacketChannel
/// </summary>
public class ChannelQueueStatistics
{
    public int TotalCount { get; init; }
    public Dictionary<PacketPriority, int> QueueSizeByPriority { get; init; } = new();
    public long TotalEnqueued { get; init; }
    public long TotalDequeued { get; init; }
    public long TotalExpired { get; init; }
    public long TotalInvalid { get; init; }
    public int UptimeSeconds { get; init; }
}
