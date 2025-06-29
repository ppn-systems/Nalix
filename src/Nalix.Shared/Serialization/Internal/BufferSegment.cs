namespace Nalix.Shared.Serialization.Internal;

/// <summary>
/// Represents a mutable buffer segment that can expand dynamically, optionally renting from the ArrayPool.
/// Designed for high-performance serialization scenarios.
/// </summary>
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Auto)]
internal struct BufferSegment
{
    private readonly bool _rent;

    private int _written;
    private byte[] _buffer;

    /// <summary>
    /// Initializes a new instance of <see cref="BufferSegment"/> with a rented buffer of the specified size.
    /// </summary>
    /// <param name="size">The initial size of the buffer.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if size is not positive.</exception>
    public BufferSegment(int size)
    {
        if (size <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");
        }

        _rent = true;
        _written = 0;
        _buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(size);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BufferSegment"/> using an existing buffer (no renting).
    /// </summary>
    /// <param name="buffer">The external buffer to use.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the buffer has zero length.</exception>
    public BufferSegment(byte[] buffer)
    {
        if (buffer.Length <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(buffer), "Size must be greater than zero.");
        }

        _rent = false;
        _written = 0;
        _buffer = buffer;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BufferSegment"/> from a span by copying it.
    /// </summary>
    /// <param name="buffer">The source span to copy.</param>
    public BufferSegment(System.Span<byte> buffer)
        : this(buffer.ToArray())
    {
    }

    /// <summary>
    /// Gets the number of bytes written to the buffer.
    /// </summary>
    public readonly int WrittenCount => _written;

    /// <summary>
    /// Gets a value indicating whether the buffer is null.
    /// </summary>
    public readonly bool IsNull => _buffer == null;

    /// <summary>
    /// Gets the total capacity of the internal buffer.
    /// </summary>
    public readonly int Length => _buffer?.Length ?? 0;

    /// <summary>
    /// Gets a span of the remaining unwritten portion of the buffer.
    /// </summary>
    public readonly System.Span<byte> FreeBuffer => System.MemoryExtensions.AsSpan(_buffer, _written);

    /// <summary>
    /// Gets a span of the written portion of the buffer.
    /// </summary>
    public readonly System.Span<byte> WrittenBuffer => System.MemoryExtensions.AsSpan(_buffer, 0, _written);

    /// <summary>
    /// Gets a memory representation of the written data.
    /// </summary>
    public readonly System.Memory<byte> WrittenMemory => System.MemoryExtensions.AsMemory(_buffer, 0, _written);

    /// <summary>
    /// Advances the write cursor by the specified count.
    /// </summary>
    /// <param name="count">The number of bytes written.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => _written += count;

    /// <summary>
    /// Ensures the buffer has enough free space, expanding if necessary.
    /// </summary>
    /// <param name="minimumSize">The minimum number of bytes required.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if minimumSize is not positive.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Expand(int minimumSize)
    {
        if (minimumSize <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(minimumSize), "Size must be greater than zero.");
        }

        if (_buffer != null && _buffer.Length - _written >= minimumSize)
        {
            return;
        }

        int newSize = System.Math.Max((_buffer?.Length ?? 0) * 2, _written + minimumSize);
        byte[] newBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(newSize);

        if (_buffer != null && _written > 0)
        {
            System.Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _written);
        }

        if (_buffer != null && _rent)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
        }

        _buffer = newBuffer;
    }

    /// <summary>
    /// Clears the buffer and returns it to the ArrayPool if rented.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_buffer != null && _rent == true)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
            _written = 0;
            _buffer = null!;
        }
    }
}
