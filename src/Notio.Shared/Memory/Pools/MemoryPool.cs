using Notio.Shared.Internal;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Notio.Shared.Memory.Pools;

/// <summary>
/// Represents memory managed by an <see cref="ArrayPool{T}"/> with efficient disposal tracking.
/// </summary>
/// <typeparam name="T">The type of the elements in the pooled array.</typeparam>
public readonly struct MemoryPool<T> : IDisposable
{
    #region Fields

    // Private fields
    private readonly T[] _array;
    private readonly int _length;
    private readonly ArrayPool<T> _pool;
    private readonly object _disposeTracker;

    #endregion

    #region Properties

    /// <summary>
    /// Gets a <see cref="ReadOnlyMemory{T}"/> representing the memory of the pooled array.
    /// </summary>
    public ReadOnlyMemory<T> Memory => new(_array, 0, _length);

    /// <summary>
    /// Gets a <see cref="Memory{T}"/> representing the writable memory of the pooled array.
    /// </summary>
    public Memory<T> WritableMemory => new(_array, 0, _length);

    /// <summary>
    /// Gets a <see cref="Span{T}"/> representing the memory of the pooled array.
    /// </summary>
    public Span<T> Span => new(_array, 0, _length);

    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> representing the memory of the pooled array.
    /// </summary>
    public ReadOnlySpan<T> ReadOnlySpan => new(_array, 0, _length);

    /// <summary>
    /// Gets the length of the usable portion of the pooled array.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets the total capacity of the pooled array.
    /// </summary>
    public int Capacity => _array.Length;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryPool{T}"/> struct.
    /// </summary>
    /// <param name="array">The array representing the memory chunk.</param>
    /// <param name="length">The length of the memory used in the array.</param>
    /// <param name="pool">The array pool from which the array was rented.</param>
    /// <exception cref="ArgumentNullException">Thrown when array or pool is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length is negative or greater than array length.</exception>
    public MemoryPool(T[] array, int length, ArrayPool<T> pool)
    {
        _array = array ?? throw new ArgumentNullException(nameof(array));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));

        if (length < 0 || length > array.Length)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be between 0 and array length.");

        _length = length;
        _disposeTracker = new DisposableTracker<T>(array, length, pool);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a new <see cref="MemoryPool{T}"/> instance with the specified length.
    /// </summary>
    /// <param name="length">The desired memory length.</param>
    /// <param name="exactLength">If true, the array will be exactly the requested length. If false, it may be larger.</param>
    /// <returns>A new <see cref="MemoryPool{T}"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryPool<T> Rent(int length, bool exactLength = false)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

        ArrayPool<T> pool = ArrayPool<T>.Shared;
        T[] array = pool.Rent(length);

        // If exactLength is true, we need to create a MemoryPool with the exact requested length
        // Otherwise, use the full rented array length
        return new MemoryPool<T>(array, exactLength ? length : array.Length, pool);
    }

    /// <summary>
    /// Creates a new <see cref="MemoryPool{T}"/> instance with the specified length from a custom pool.
    /// </summary>
    /// <param name="length">The desired memory length.</param>
    /// <param name="customPool">The custom array pool to rent from.</param>
    /// <param name="exactLength">If true, the array will be exactly the requested length. If false, it may be larger.</param>
    /// <returns>A new <see cref="MemoryPool{T}"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when customPool is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryPool<T> Rent(int length, ArrayPool<T> customPool, bool exactLength = false)
    {
        ArgumentNullException.ThrowIfNull(customPool);
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

        T[] array = customPool.Rent(length);

        // If exactLength is true, we need to create a MemoryPool with the exact requested length
        // Otherwise, use the full rented array length
        return new MemoryPool<T>(array, exactLength ? length : array.Length, customPool);
    }

    /// <summary>
    /// Returns an element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element to return.</param>
    /// <returns>The element at the specified index.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when the index is out of range.</exception>
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_length)
                throw new IndexOutOfRangeException();
            return _array[index];
        }
    }

    /// <summary>
    /// Copies the contents of the pooled memory to a destination span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <exception cref="ArgumentException">Thrown when the destination is too small.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<T> destination) => ReadOnlySpan.CopyTo(destination);

    /// <summary>
    /// Creates a new array with a copy of the pooled memory contents.
    /// </summary>
    /// <returns>A new array containing copies of the elements.</returns>
    public T[] ToArray() => ReadOnlySpan.ToArray();

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases the pooled memory back to the <see cref="ArrayPool{T}"/> and clears the array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        // We use the dispose tracker to handle the actual disposal
        // This allows us to maintain a readonly struct while still supporting proper disposal
        if (_disposeTracker is DisposableTracker<T> tracker)
        {
            tracker.Dispose();
        }
    }

    #endregion
}
