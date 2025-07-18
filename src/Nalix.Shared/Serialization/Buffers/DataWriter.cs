namespace Nalix.Shared.Serialization.Buffers;

/// <summary>
/// Represents a mutable buffer segment that can expand dynamically, optionally renting from the ArrayPool.
/// Designed for high-performance serialization scenarios.
/// </summary>
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Auto)]
public struct DataWriter
{
    private readonly System.Boolean _rent;
    private System.Byte[] _buffer;

    /// <summary>
    /// Initializes a new instance of <see cref="DataWriter"/> with a rented buffer of the specified size.
    /// </summary>
    /// <param name="size">The initial size of the buffer.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if size is not positive.</exception>
    public DataWriter(System.Int32 size)
    {
        if (size <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");
        }

        _rent = true;
        WrittenCount = 0;
        _buffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(size);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataWriter"/> using an existing buffer (no renting).
    /// </summary>
    /// <param name="buffer">The external buffer to use.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the buffer has zero length.</exception>
    public DataWriter(System.Byte[] buffer)
    {
        if (buffer.Length <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(buffer), "Size must be greater than zero.");
        }

        _rent = false;
        WrittenCount = 0;
        _buffer = buffer;
    }

    /// <summary>
    /// Gets the number of bytes written to the buffer.
    /// </summary>
    public System.Int32 WrittenCount { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the buffer is null.
    /// </summary>
    public readonly System.Boolean IsNull => _buffer == null;

    /// <summary>
    /// Gets the total capacity of the internal buffer.
    /// </summary>
    public readonly System.Int32 Length => _buffer?.Length ?? 0;

    /// <summary>
    /// Gets a span of the remaining unwritten portion of the buffer.
    /// </summary>
    public readonly System.Span<System.Byte> FreeBuffer
        => System.MemoryExtensions.AsSpan(_buffer, WrittenCount);

    /// <summary>
    /// Gets a span of the written portion of the buffer.
    /// </summary>
    public readonly System.Span<System.Byte> WrittenBuffer
        => System.MemoryExtensions.AsSpan(_buffer, 0, WrittenCount);

    /// <summary>
    /// Gets a memory representation of the written data.
    /// </summary>
    public readonly System.Memory<System.Byte> WrittenMemory
        => System.MemoryExtensions.AsMemory(_buffer, 0, WrittenCount);

    /// <summary>
    /// Advances the write cursor by the specified count.
    /// </summary>
    /// <param name="count">The number of bytes written.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Advance(System.Int32 count)
    {
        if (count <= 0 || WrittenCount + count > _buffer.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(count), "Advance out of buffer bounds.");
        }

        WrittenCount += count;
    }

    /// <summary>
    /// Retrieves a reference to the first byte in the free buffer space.
    /// </summary>
    /// <returns>A reference to the first byte in the free buffer.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly ref System.Byte GetFreeBufferReference()
        => ref System.Runtime.InteropServices.MemoryMarshal.GetReference(FreeBuffer);

    /// <summary>
    /// Ensures the buffer has enough free space, expanding if necessary.
    /// </summary>
    /// <param name="minimumSize">The minimum number of bytes required.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if minimumSize is not positive.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Expand(System.Int32 minimumSize)
    {
        if (minimumSize <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(minimumSize), "Size must be greater than zero.");
        }

        if (_buffer != null && _buffer.Length - WrittenCount >= minimumSize)
        {
            return;
        }

        if (!_rent)
        {
            throw new System.InvalidOperationException("Cannot expand a fixed buffer.");
        }

        System.Int32 newSize = System.Math.Max((_buffer?.Length ?? 0) * 2, WrittenCount + minimumSize);
        System.Byte[] newBuffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(newSize);

        if (_buffer != null && WrittenCount > 0)
        {
            System.Buffer.BlockCopy(_buffer, 0, newBuffer, 0, WrittenCount);
        }

        if (_buffer != null && _rent)
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(_buffer);
        }

        _buffer = newBuffer;
    }

    /// <summary>
    /// Retrieves a reference to the first byte in the free buffer space with the specified size hint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly System.Byte[] ToArray() => WrittenCount == 0 ? [] : _buffer[..WrittenCount];

    /// <summary>
    /// Clears the buffer and returns it to the ArrayPool if rented.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_buffer != null)
        {
            if (_rent)
            {
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(_buffer);
            }

            WrittenCount = 0;
            _buffer = null!;
        }
    }
}