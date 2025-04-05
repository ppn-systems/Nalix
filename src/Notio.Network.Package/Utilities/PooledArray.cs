using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Utilities;

/// <summary>
/// Manages the return of an array to the specified array pool upon disposal.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PooledArray"/> class.
/// </remarks>
/// <param name="array">The array to be returned to the pool.</param>
/// <param name="pool">The array pool to return the array to.</param>
internal sealed class PooledArray(byte[] array, ArrayPool<byte> pool) : IDisposable
{
    private readonly byte[] _array = array;
    private readonly ArrayPool<byte> _pool = pool;
    private bool _disposed;

    /// <summary>
    /// Returns the array to the pool and clears it if not already disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (!_disposed)
        {
            _pool.Return(_array, clearArray: true);
            _disposed = true;
        }
    }
}
