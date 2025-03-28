using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Shared.Internal;

/// <summary>
/// Helper class to track disposal state and handle the actual disposal of the array.
/// </summary>
internal sealed class DisposableTracker<T>(T[] array, int length, ArrayPool<T> pool) : IDisposable
{
    private int _disposed;
    private T[]? _array = array;
    private readonly int _length = length;
    private readonly ArrayPool<T> _pool = pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        T[]? array = _array;

        if (array == null)
            return;

        _array = null;

        // Clear the array before returning it to the pool
        Array.Clear(array, 0, _length);
        _pool.Return(array);
    }
}
