using System;
using System.Buffers;

namespace Notio.Shared.Memory.Pool;

/// <summary>
/// Đại diện cho bộ nhớ được quản lý từ ArrayPool.
/// </summary>
public readonly struct PooledMemory<T>(T[] array, int length, ArrayPool<T> pool) : IDisposable
{
    private readonly T[] _array = array;
    private readonly int _length = length;
    private readonly ArrayPool<T> _pool = pool;

    public ReadOnlyMemory<T> Memory => new(_array, 0, _length);

    public void Dispose()
    {
        if (_array != null)
        {
            Array.Clear(_array, 0, _length);
            _pool.Return(_array);
        }
    }
}