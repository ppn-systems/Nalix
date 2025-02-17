using System;
using System.Buffers;

namespace Notio.Shared.Memory;

/// <summary>
/// Represents memory managed by an <see cref="ArrayPool{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the elements in the pooled array.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="PooledMemory{T}"/> struct.
/// </remarks>
/// <param name="array">The array representing the memory chunk.</param>
/// <param name="length">The length of the memory used in the array.</param>
/// <param name="pool">The array pool from which the array was rented.</param>
public readonly struct PooledMemory<T>(T[] array, int length, ArrayPool<T> pool) : IDisposable
{
    private readonly T[] _array = array ?? throw new ArgumentNullException(nameof(array));
    private readonly int _length = length;
    private readonly ArrayPool<T> _pool = pool ?? throw new ArgumentNullException(nameof(pool));

    /// <summary>
    /// Gets a <see cref="ReadOnlyMemory{T}"/> representing the memory of the pooled array.
    /// </summary>
    public ReadOnlyMemory<T> Memory => new(_array, 0, _length);

    /// <summary>
    /// Releases the pooled memory back to the <see cref="ArrayPool{T}"/> and clears the array.
    /// </summary>
    public void Dispose()
    {
        if (_array == null) return;
        
        Array.Clear(_array, 0, _length);
        _pool.Return(_array);
    }
}
