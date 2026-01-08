// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Internal;
using System;

namespace Nalix.Shared.Memory.Pools;

/// <summary>
/// Represents memory managed by an <see cref="System.Buffers.ArrayPool{T}"/> with efficient disposal tracking.
/// </summary>
/// <typeparam name="T">The type of the elements in the pooled array.</typeparam>
public readonly struct MemoryPool<T> : System.IDisposable
{
    #region Fields

    // Private fields
    private readonly T[] _array;
    private readonly System.Object _disposeTracker;
    private readonly System.Buffers.ArrayPool<T> _pool;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets a <see cref="System.ReadOnlyMemory{T}"/> representing the memory of the pooled array.
    /// </summary>
    public System.ReadOnlyMemory<T> Memory => new(_array, 0, Length);

    /// <summary>
    /// Gets a <see cref="System.Memory{T}"/> representing the writable memory of the pooled array.
    /// </summary>
    public System.Memory<T> WritableMemory => new(_array, 0, Length);

    /// <summary>
    /// Gets a <see cref="System.Span{T}"/> representing the memory of the pooled array.
    /// </summary>
    public System.Span<T> Span => new(_array, 0, Length);

    /// <summary>
    /// Gets a <see cref="System.ReadOnlySpan{T}"/> representing the memory of the pooled array.
    /// </summary>
    public System.ReadOnlySpan<T> ReadOnlySpan => new(_array, 0, Length);

    /// <summary>
    /// Gets the length of the usable portion of the pooled array.
    /// </summary>
    public System.Int32 Length { get; }

    /// <summary>
    /// Gets the total capacity of the pooled array.
    /// </summary>
    public System.Int32 Capacity => _array.Length;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryPool{T}"/> struct.
    /// </summary>
    /// <param name="array">The array representing the memory chunk.</param>
    /// <param name="length">The length of the memory used in the array.</param>
    /// <param name="pool">The array pool from which the array was rented.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when array or pool is null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when length is negative or greater than array length.</exception>
    public MemoryPool(T[] array, System.Int32 length, System.Buffers.ArrayPool<T> pool)
    {
        _array = array ?? throw new System.ArgumentNullException(nameof(array));
        _pool = pool ?? throw new System.ArgumentNullException(nameof(pool));

        if (length < 0 || length > array.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(length), "Length must be between 0 and array length.");
        }

        Length = length;
        _disposeTracker = new DisposableTracker<T>(array, length, pool);
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Creates a new <see cref="MemoryPool{T}"/> instance with the specified length.
    /// </summary>
    /// <param name="length">The desired memory length.</param>
    /// <param name="exactLength">If true, the array will be exactly the requested length. If false, it may be larger.</param>
    /// <returns>A new <see cref="MemoryPool{T}"/> instance.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when length is negative.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static MemoryPool<T> Rent(System.Int32 length, System.Boolean exactLength = false)
    {
        if (length < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
        }

        System.Buffers.ArrayPool<T> pool = System.Buffers.ArrayPool<T>.Shared;
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
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when length is negative.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when customPool is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static MemoryPool<T> Rent(
        System.Int32 length,
        System.Buffers.ArrayPool<T> customPool, System.Boolean exactLength = false)
    {
        System.ArgumentNullException.ThrowIfNull(customPool);
        if (length < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
        }

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
    /// <exception cref="System.IndexOutOfRangeException">Thrown when the index is out of range.</exception>
    public T this[System.Int32 index]
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => (System.UInt32)index >= (System.UInt32)Length ? throw new System.IndexOutOfRangeException() : _array[index];
    }

    /// <summary>
    /// Copies the contents of the pooled memory to a destination span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <exception cref="System.ArgumentException">Thrown when the destination is too small.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void CopyTo(System.Span<T> destination) => ReadOnlySpan.CopyTo(destination);

    /// <summary>
    /// Creates a new array with a copy of the pooled memory contents.
    /// </summary>
    /// <returns>A new array containing copies of the elements.</returns>
    public T[] ToArray() => ReadOnlySpan.ToArray();

    #endregion Public Methods

    #region IDisposable

    /// <summary>
    /// Releases the pooled memory back to the <see cref="System.Buffers.ArrayPool{T}"/> and clears the array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        // We use the dispose tracker to handle the actual disposal
        // This allows us to maintain a readonly struct while still supporting proper disposal
        if (_disposeTracker is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    #endregion IDisposable
}
