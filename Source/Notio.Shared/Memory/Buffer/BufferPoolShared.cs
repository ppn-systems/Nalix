using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Quản lý một pool của các bộ đệm dùng chung.
/// </summary>
public sealed class BufferPoolShared : IDisposable
{
    private static readonly ConcurrentDictionary<int, BufferPoolShared> _pools = new();
    private readonly ConcurrentQueue<byte[]> _freeBuffers;
    private readonly Lock _disposeLock = new();
    private readonly ArrayPool<byte> _arrayPool;
    private readonly int _bufferSize;

    private BufferPoolInfo _poolInfo;
    private int _totalBuffers;
    private bool _disposed;
    private int _misses;

    /// <summary>
    /// Tổng số lượng bộ đệm trong pool.
    /// </summary>
    public int TotalBuffers => _totalBuffers;

    /// <summary>
    /// Số lượng bộ đệm rảnh trong pool.
    /// </summary>
    public int FreeBuffers => _freeBuffers.Count;

    /// <summary>
    /// Khởi tạo một thể hiện mới của lớp <see cref="BufferPoolShared"/>.
    /// </summary>
    /// <param name="bufferSize">Kích thước của mỗi bộ đệm trong pool.</param>
    /// <param name="initialCapacity">Số lượng bộ đệm ban đầu để cấp phát.</param>
    private BufferPoolShared(int bufferSize, int initialCapacity)
    {
        _bufferSize = bufferSize;
        _arrayPool = ArrayPool<byte>.Shared;
        _freeBuffers = new ConcurrentQueue<byte[]>();

        for (int i = 0; i < initialCapacity; ++i)
        {
            _freeBuffers.Enqueue(_arrayPool.Rent(bufferSize));
        }

        _totalBuffers = initialCapacity;
    }

    /// <summary>
    /// Lấy hoặc tạo một pool bộ đệm chung cho kích thước bộ đệm chỉ định.
    /// </summary>
    /// <param name="bufferSize">Kích thước của mỗi bộ đệm trong pool.</param>
    /// <param name="initialCapacity">Số lượng bộ đệm ban đầu để cấp phát.</param>
    /// <returns>Đối tượng <see cref="BufferPoolShared"/> cho kích thước bộ đệm chỉ định.</returns>
    public static BufferPoolShared GetOrCreatePool(int bufferSize, int initialCapacity)
    {
        return _pools.GetOrAdd(bufferSize, size => new BufferPoolShared(size, initialCapacity));
    }

    /// <summary>
    /// Lấy một bộ đệm từ pool.
    /// </summary>
    /// <returns>Một mảng byte của bộ đệm.</returns>
    public byte[] AcquireBuffer()
    {
        if (_freeBuffers.TryDequeue(out var buffer))
        {
            return buffer;
        }

        Interlocked.Increment(ref _misses);
        Interlocked.Increment(ref _totalBuffers);

        return _arrayPool.Rent(_bufferSize);
    }

    /// <summary>
    /// Trả lại một bộ đệm vào pool.
    /// </summary>
    /// <param name="buffer">Bộ đệm để trả lại.</param>
    public void ReleaseBuffer(byte[] buffer)
    {
        if (buffer == null || buffer.Length != _bufferSize)
        {
            throw new ArgumentException("Invalid buffer.");
        }

        _freeBuffers.Enqueue(buffer);
    }

    /// <summary>
    /// Tăng dung lượng pool bằng cách thêm các bộ đệm.
    /// </summary>
    /// <param name="additionalCapacity">Số lượng bộ đệm thêm vào.</param>
    public void IncreaseCapacity(int additionalCapacity)
    {
        if (additionalCapacity <= 0)
        {
            throw new ArgumentException("The additional quantity must be greater than no.");
        }

        var buffersToAdd = new List<byte[]>(additionalCapacity);
        for (int i = 0; i < additionalCapacity; ++i)
        {
            buffersToAdd.Add(_arrayPool.Rent(_bufferSize));
        }

        foreach (var buffer in buffersToAdd)
        {
            _freeBuffers.Enqueue(buffer);
        }

        Interlocked.Add(ref _totalBuffers, additionalCapacity);
    }

    /// <summary>
    /// Giảm dung lượng pool bằng cách loại bỏ các bộ đệm.
    /// </summary>
    /// <param name="capacityToRemove">Số lượng bộ đệm để loại bỏ.</param>
    public void DecreaseCapacity(int capacityToRemove)
    {
        if (capacityToRemove <= 0)
        {
            return;
        }

        for (int i = 0; i < capacityToRemove; ++i)
        {
            if (_freeBuffers.TryDequeue(out var buffer))
            {
                _arrayPool.Return(buffer);
                Interlocked.Decrement(ref _totalBuffers);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Lấy thông tin về pool bộ đệm và lưu trữ thông tin này trong một field private.
    /// </summary>
    /// <returns>Tham chiếu chỉ đọc tới BufferPoolInfo.</returns>
    public BufferPoolInfo GetPoolInfo()
    {
        return new BufferPoolInfo
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = _totalBuffers,
            BufferSize = _bufferSize,
            Misses = _misses
        };
    }

    /// <summary>
    /// Lấy thông tin về pool bộ đệm và lưu trữ thông tin này trong một field private.
    /// </summary>
    /// <returns>Tham chiếu chỉ đọc tới BufferPoolInfo.</returns>
    public ref readonly BufferPoolInfo GetPoolInfoRef()
    {
        // Cache thông tin pool trong một field private
        _poolInfo = new BufferPoolInfo
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = _totalBuffers,
            BufferSize = _bufferSize,
            Misses = _misses
        };

        return ref _poolInfo;
    }

    /// <summary>
    /// Giải phóng pool bộ đệm và trả lại tất cả các bộ đệm vào pool mảng.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Thực hiện giải phóng tài nguyên.
    /// </summary>
    /// <param name="disposing">Chỉ định liệu việc giải phóng có được gọi từ Dispose hay không.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        lock (_disposeLock)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Giải phóng tài nguyên được quản lý
                while (_freeBuffers.TryDequeue(out var buffer))
                {
                    _arrayPool.Return(buffer);
                }

                _pools.TryRemove(_bufferSize, out _);
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer (chỉ sử dụng nếu cần giải phóng tài nguyên không được quản lý).
    /// </summary>
    ~BufferPoolShared()
    {
        Dispose(disposing: false);
    }
}