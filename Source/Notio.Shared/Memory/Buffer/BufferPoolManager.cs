using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Quản lý các pool của các bộ đệm dùng chung.
/// </summary>
public sealed class BufferPoolManager : IDisposable
{
    private readonly ConcurrentDictionary<int, BufferPoolShared> _pools = new();
    private readonly ConcurrentDictionary<int, (int RentCounter, int ReturnCounter)> _adjustmentCounters = new();
    private int[] _sortedKeys = [];

    /// <summary>
    /// Sự kiện kích hoạt khi cần tăng dung lượng bộ đệm.
    /// </summary>
    public event Action<BufferPoolShared>? EventIncrease;

    /// <summary>
    /// Sự kiện kích hoạt khi cần giảm dung lượng bộ đệm.
    /// </summary>
    public event Action<BufferPoolShared>? EventShrink;

    /// <summary>
    /// Tạo một pool bộ đệm mới với kích thước và dung lượng ban đầu cho trước.
    /// </summary>
    /// <param name="bufferSize">Kích thước của mỗi bộ đệm trong pool.</param>
    /// <param name="initialCapacity">Số lượng bộ đệm ban đầu để cấp phát.</param>
    public void CreatePool(int bufferSize, int initialCapacity)
    {
        if (_pools.TryAdd(bufferSize, BufferPoolShared.GetOrCreatePool(bufferSize, initialCapacity)))
        {
            // Cập nhật danh sách kích thước đã sắp xếp
            _sortedKeys = [.. _pools.Keys.OrderBy(k => k)];
        }
    }

    /// <summary>
    /// Thuê một bộ đệm có ít nhất kích thước yêu cầu.
    /// </summary>
    /// <param name="size">Kích thước của bộ đệm cần thuê.</param>
    /// <returns>Một mảng byte của bộ đệm.</returns>
    /// <exception cref="ArgumentException">Ném ra nếu kích thước bộ đệm yêu cầu vượt quá kích thước pool có sẵn.</exception>
    public byte[] RentBuffer(int size)
    {
        int poolSize = FindSuitablePoolSize(size);
        if (poolSize == 0)
            throw new ArgumentException("Requested buffer size exceeds maximum available pool size.");

        BufferPoolShared pool = _pools[poolSize];
        byte[] buffer = pool.AcquireBuffer();

        // Kiểm tra và kích hoạt sự kiện tăng dung lượng
        if (AdjustCounter(poolSize, isRent: true))
            EventIncrease?.Invoke(pool);

        return buffer;
    }

    /// <summary>
    /// Trả lại bộ đệm về pool thích hợp.
    /// </summary>
    /// <param name="buffer">Bộ đệm để trả lại.</param>
    /// <exception cref="ArgumentException">Ném ra nếu kích thước bộ đệm không hợp lệ.</exception>
    public void ReturnBuffer(byte[] buffer)
    {
        if (buffer == null || !_pools.TryGetValue(buffer.Length, out BufferPoolShared? pool))
            throw new ArgumentException("Invalid buffer size.");

        pool.ReleaseBuffer(buffer);

        // Kiểm tra và kích hoạt sự kiện giảm dung lượng
        if (AdjustCounter(buffer.Length, isRent: false))
            EventShrink?.Invoke(pool);
    }

    /// <summary>
    /// Điều chỉnh bộ đếm thuê và trả bộ đệm, đồng thời kiểm tra xem có nên kích hoạt sự kiện tăng/giảm dung lượng hay không.
    /// </summary>
    /// <param name="poolSize">Kích thước của pool.</param>
    /// <param name="isRent">Xác định xem là hành động thuê hay trả bộ đệm.</param>
    /// <returns>True nếu nên kích hoạt sự kiện, ngược lại False.</returns>
    private bool AdjustCounter(int poolSize, bool isRent)
    {
        return _adjustmentCounters.AddOrUpdate(poolSize,
            key => isRent ? (1, 0) : (0, 1),
            (key, current) =>
            {
                var newRentCounter = isRent ? current.RentCounter + 1 : current.RentCounter;
                var newReturnCounter = isRent ? current.ReturnCounter : current.ReturnCounter + 1;

                if (newRentCounter >= 10 && isRent)
                    return (0, current.ReturnCounter);

                if (newReturnCounter >= 10 && !isRent)
                    return (current.RentCounter, 0);

                return (newRentCounter, newReturnCounter);
            }).RentCounter == 0 || _adjustmentCounters[poolSize].ReturnCounter == 0;
    }

    /// <summary>
    /// Tìm kích thước pool phù hợp nhất dựa trên kích thước yêu cầu.
    /// </summary>
    /// <param name="size">Kích thước yêu cầu.</param>
    /// <returns>Kích thước của pool phù hợp.</returns>
    private int FindSuitablePoolSize(int size)
    {
        foreach (var key in _sortedKeys)
        {
            if (key >= size)
                return key;
        }
        return 0;
    }

    /// <summary>
    /// Giải phóng tất cả các tài nguyên của các pool bộ đệm.
    /// </summary>
    public void Dispose()
    {
        foreach (var pool in _pools.Values)
        {
            pool.Dispose();
        }
        _pools.Clear();
        _sortedKeys = [];
    }
}