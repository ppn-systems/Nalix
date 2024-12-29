using Notio.Common.IMemory;
using Notio.Shared.Configuration;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using Notio.Common;
using Notio.Shared.Memory.Buffer;
using Notio.Common.Metadata;

namespace Notio.Shared.Memory;

/// <summary>
/// Quản lý các bộ đệm có nhiều kích thước khác nhau.
/// </summary>
public sealed class BufferAllocator : IBufferAllocator
{
    private const int MinimumIncrease = 4;
    private const int MaxBufferIncreaseLimit = 1024;

    private bool _isInitialized;
    private readonly int _totalBuffers;
    private readonly (int BufferSize, double Allocation)[] _bufferAllocations;
    private readonly BufferPoolManager _poolManager = new();

    public BufferConfig BufferConfig { get; } = ConfigManager.Instance.GetConfig<BufferConfig>();

    /// <summary>
    /// Sự kiện theo dõi.
    /// </summary>
    public event Action<string, EventId>? TraceOccurred;

    /// <summary>
    /// Khởi tạo một thể hiện mới của lớp <see cref="BufferAllocator"/> với các cấu hình phân bổ bộ đệm và tổng số bộ đệm.
    /// </summary>
    public BufferAllocator(BufferConfig? bufferConfig = null)
    {
        if (bufferConfig is not null)
        {
            BufferConfig = bufferConfig;
        }

        _totalBuffers = BufferConfig.TotalBuffers;
        _bufferAllocations = ParseBufferAllocations(BufferConfig.BufferAllocations);

        _poolManager.EventShrink += ShrinkBufferPoolSize;
        _poolManager.EventIncrease += IncreaseBufferPoolSize;
    }

    /// <summary>
    /// Cấp phát các bộ đệm dựa trên cấu hình.
    /// </summary>
    /// <exception cref="InvalidOperationException">Ném ra nếu bộ đệm đã được cấp phát.</exception>
    public void AllocateBuffers()
    {
        if (_isInitialized) throw new InvalidOperationException("Buffers already allocated.");

        foreach (var (bufferSize, allocation) in _bufferAllocations)
        {
            int capacity = (int)(_totalBuffers * allocation);
            _poolManager.CreatePool(bufferSize, capacity);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Thuê một bộ đệm có ít nhất kích thước yêu cầu.
    /// </summary>
    /// <param name="size">Kích thước của bộ đệm cần thuê, mặc định là 256.</param>
    /// <returns>Một mảng byte của bộ đệm.</returns>
    public byte[] RentBuffer(int size = 256) => _poolManager.RentBuffer(size);

    /// <summary>
    /// Trả lại bộ đệm về bộ đệm thích hợp.
    /// </summary>
    /// <param name="buffer">Bộ đệm để trả lại.</param>
    public void ReturnBuffer(byte[] buffer) => _poolManager.ReturnBuffer(buffer);

    /// <summary>
    /// Lấy tỷ lệ phân bổ cho kích thước bộ đệm cho trước.
    /// </summary>
    /// <param name="size">Kích thước của bộ đệm.</param>
    /// <returns>Tỷ lệ phân bổ của kích thước bộ đệm.</returns>
    /// <exception cref="ArgumentException">Ném ra nếu không tìm thấy phân bổ cho kích thước bộ đệm.</exception>
    public double GetAllocationForSize(int size)
    {
        foreach (var (bufferSize, allocation) in _bufferAllocations.OrderBy(alloc => alloc.BufferSize))
        {
            if (bufferSize >= size)
                return allocation;
        }

        throw new ArgumentException($"No allocation found for size: {size}");
    }

    private static (int, double)[] ParseBufferAllocations(string bufferAllocationsString)
    {
        if (string.IsNullOrWhiteSpace(bufferAllocationsString))
        {
            throw new ArgumentException(
                "The input string must not be left blank or contain only white spaces.",
                nameof(bufferAllocationsString));
        }

        try
        {
            return bufferAllocationsString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair =>
                {
                    var parts = pair.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                    {
                        throw new FormatException($"Incorrectly formatted pairs: '{pair}'");
                    }

                    return (
                        int.Parse(parts[0].Trim()),
                        double.Parse(parts[1].Trim())
                    );
                })
                .ToArray();
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is OverflowException)
        {
            throw new ArgumentException("The input string is malformed or contains invalid values.", ex);
        }
    }

    /// <summary>
    /// Giảm dung lượng bộ đệm với thuật toán tối ưu.
    /// </summary>
    private void ShrinkBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolInfo poolInfo = ref pool.GetPoolInfoRef();

        double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        int targetBuffers = (int)(targetAllocation * _totalBuffers);
        int minimumBuffers = poolInfo.TotalBuffers >> 2;

        int excessBuffers = poolInfo.FreeBuffers - targetBuffers;
        int safetyMargin = Math.Min(20, minimumBuffers);

        int buffersToShrink = Math.Clamp(
            excessBuffers - safetyMargin,
            0,
            20
        );

        if (buffersToShrink > 0)
        {
            SpinLock spinLock = new(false);
            bool lockTaken = false;

            try
            {
                spinLock.Enter(ref lockTaken);
                pool.DecreaseCapacity(buffersToShrink);

                TraceOccurred?.Invoke(
                    $"Shrank buffer pool for size {poolInfo.BufferSize}, " +
                    $"reduced by {buffersToShrink}, " +
                    $"new capacity: {poolInfo.TotalBuffers - buffersToShrink}.",
                    AppEvents.Buffer.Shrink);
            }
            finally
            {
                if (lockTaken) spinLock.Exit();
            }
        }
        else
        {
            TraceOccurred?.Invoke(
                $"No buffers were shrunk for pool size {poolInfo.BufferSize}. " +
                $"Current capacity is optimal.",
                AppEvents.Buffer.Shrink);
        }
    }


    /// <summary>
    /// Tăng dung lượng bộ đệm với thuật toán tối ưu.
    /// </summary>
    private void IncreaseBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolInfo poolInfo = ref pool.GetPoolInfoRef();

        int threshold = poolInfo.TotalBuffers >> 2; // 25% threshold

        if (poolInfo.FreeBuffers <= threshold)
        {
            // Tối ưu phép tính increaseBy
            int baseIncrease = (int)Math.Max(
                MinimumIncrease,
                BitOperations.RoundUpToPowerOf2((uint)poolInfo.TotalBuffers) >> 2
            );

            // Giới hạn tăng trưởng để tránh OOM
            int maxIncrease = Math.Min(
                baseIncrease,
                MaxBufferIncreaseLimit
            );

            SpinLock spinLock = new(false);
            bool lockTaken = false;

            try
            {
                spinLock.Enter(ref lockTaken);

                if (pool.FreeBuffers <= threshold)
                {
                    pool.IncreaseCapacity(maxIncrease);

                    TraceOccurred?.Invoke(
                        $"Increased buffer pool for size {poolInfo.BufferSize}, " +
                        $"added {maxIncrease}, " +
                        $"new capacity: {poolInfo.TotalBuffers + maxIncrease}.",
                        AppEvents.Buffer.Increase);
                }
            }
            finally
            {
                if (lockTaken) spinLock.Exit();
            }
        }
        else
        {
            // Logging
            TraceOccurred?.Invoke(
                $"No increase needed for pool size {poolInfo.BufferSize}. " +
                $"Free buffers: {poolInfo.FreeBuffers}, " +
                $"threshold: {threshold}.",
                AppEvents.Buffer.Increase);
        }
    }

    /// <summary>
    /// Giải phóng tất cả các tài nguyên của các pool bộ đệm.
    /// </summary>
    public void Dispose() => _poolManager.Dispose();
}