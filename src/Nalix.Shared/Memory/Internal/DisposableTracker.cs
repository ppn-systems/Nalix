using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Shared.Memory.Internal;

/// <summary>
/// Helper class to track disposal state and handle the actual disposal of the array.
/// </summary>
internal sealed class DisposableTracker<T>(T[] array, Int32 length, ArrayPool<T> pool) : IDisposable
{
    #region Fields

    private Int32 _disposed;
    private T[]? _array = array;
    private readonly Int32 _length = length;
    private readonly ArrayPool<T> _pool = pool;

    #endregion Fields

    #region IDisposable

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        T[]? array = _array;

        if (array == null)
        {
            return;
        }

        _array = null;

        // Dispose the array before returning it to the pool
        Array.Clear(array, 0, _length);
        _pool.Return(array);
    }

    #endregion IDisposable
}
