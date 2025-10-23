// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Internal;

/// <summary>
/// Helper class to track disposal state and handle the actual disposal of the array.
/// </summary>
[System.Diagnostics.DebuggerDisplay("Length={_length}, Disposed={_disposed != 0}")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class DisposableTracker<T>(
    T[] array, System.Int32 length,
    System.Buffers.ArrayPool<T> pool) : System.IDisposable
{
    #region Fields

    private System.Int32 _disposed;
    private T[]? _array = array;
    private readonly System.Int32 _length = length;
    private readonly System.Buffers.ArrayPool<T> _pool = pool;

    #endregion Fields

    #region IDisposable

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
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
        System.Array.Clear(array, 0, _length);
        _pool.Return(array);
    }

    #endregion IDisposable
}
