using Nalix.Common.Exceptions;

namespace Nalix.Shared.Serialization.Buffers;

/// <summary>
/// Provides functionality for reading serialized data from a byte buffer.
/// Supports managed <c>byte[]</c>, <c>ReadOnlyMemory&lt;byte&gt;</c>, <c>ReadOnlySpan&lt;byte&gt;</c>, and unmanaged memory.
/// </summary>
public unsafe struct DataReader : System.IDisposable
{
    private System.Byte* _ptr;
    private System.Int32 _length;
    private System.Int32 _position;
    private System.Boolean _pinned;
    private System.Runtime.InteropServices.GCHandle _pin; // Used only when the source is a byte array

    /// <summary>
    /// Gets the number of bytes that have been consumed from the buffer.
    /// </summary>
    public readonly System.Int32 BytesRead => _position;

    /// <summary>
    /// Gets the number of bytes remaining in the buffer.
    /// </summary>
    public readonly System.Int32 BytesRemaining => _length - _position;

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a managed byte array.
    /// This ensures safety by pinning the array to prevent movement by the garbage collector.
    /// </summary>
    /// <param name="buffer">The byte array to read from.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if the provided buffer is null.</exception>
    public DataReader(System.Byte[] buffer)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);
        _pin = System.Runtime.InteropServices.GCHandle.Alloc(
            buffer,
            System.Runtime.InteropServices.GCHandleType.Pinned);

        _ptr = (System.Byte*)_pin.AddrOfPinnedObject();
        _length = buffer.Length;
        _position = 0;
        _pinned = true;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for an unmanaged memory pointer.
    /// This is useful for reading from native memory, stack allocations, or manually allocated buffers.
    /// </summary>
    /// <param name="ptr">The unmanaged memory pointer.</param>
    /// <param name="length">The length of the buffer.</param>
    public DataReader(System.Byte* ptr, System.Int32 length)
    {
        _ptr = ptr;
        _length = length;
        _position = 0;
        _pin = default;
        _pinned = false;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a read-only span of bytes.
    /// This constructor creates a pinned copy of the span to ensure safe access to its data.
    /// </summary>
    /// <param name="span">The read-only span of bytes to read from.</param>
    public DataReader(System.ReadOnlySpan<System.Byte> span)
    {
        System.Byte[] temp = span.ToArray();
        _pin = System.Runtime.InteropServices.GCHandle.Alloc(
            temp,
            System.Runtime.InteropServices.GCHandleType.Pinned);

        _ptr = (System.Byte*)_pin.AddrOfPinnedObject();
        _length = temp.Length;
        _pinned = true;
        _position = 0;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a read-only span of bytes.
    /// This constructor creates a pinned copy of the span to ensure safe access to its data.
    /// </summary>
    /// <param name="memory">The read-only memory of bytes to read from.</param>
    public DataReader(System.ReadOnlyMemory<System.Byte> memory)
    {
        if (System.Runtime.InteropServices.MemoryMarshal
            .TryGetArray(memory, out System.ArraySegment<System.Byte> segment))
        {
            _pin = System.Runtime.InteropServices.GCHandle.Alloc(
                segment.Array!,
                System.Runtime.InteropServices.GCHandleType.Pinned);
            _ptr = (System.Byte*)_pin.AddrOfPinnedObject() + segment.Offset;
            _length = segment.Count;
            _pinned = true;
        }
        else
        {
            // fallback: allocate + copy
            System.Byte[] temp = memory.ToArray();
            _pin = System.Runtime.InteropServices.GCHandle.Alloc(
                temp,
                System.Runtime.InteropServices.GCHandleType.Pinned);
            _ptr = (System.Byte*)_pin.AddrOfPinnedObject();
            _length = temp.Length;
            _pinned = true;
        }
        _position = 0;
    }

    /// <summary>
    /// Retrieves a read-only span of bytes from the current position in the buffer.
    /// </summary>
    /// <param name="length">The number of bytes to retrieve.</param>
    /// <returns>A span containing the requested bytes.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the requested length exceeds the available buffer size.
    /// </exception>
    public readonly System.ReadOnlySpan<System.Byte> GetSpan(System.Int32 length)
    {
        if (length > BytesRemaining)
            throw new SerializationException(
                $"Not enough data: requested {length} bytes, only {BytesRemaining} bytes remaining.");

        return new System.ReadOnlySpan<System.Byte>(_ptr + _position, length);
    }

    /// <summary>
    /// Retrieves a reference to the first byte in the requested span.
    /// </summary>
    /// <param name="sizeHint">The minimum number of bytes required.</param>
    /// <returns>A reference to the first byte of the span.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the requested size exceeds the available buffer size.
    /// </exception>
    public readonly ref System.Byte GetSpanReference(System.Int32 sizeHint)
    {
        if (sizeHint > BytesRemaining)
            throw new SerializationException(
                $"Not enough data: requested {sizeHint} bytes, only {BytesRemaining} bytes remaining.");

        return ref *(_ptr + _position);
    }

    /// <summary>
    /// Advances the read position in the buffer by the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to advance.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the advance count is negative.</exception>
    /// <exception cref="SerializationException">
    /// Thrown if the advance count exceeds the available buffer size.
    /// </exception>
    public void Advance(System.Int32 count)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > BytesRemaining)
            throw new SerializationException(
                $"Cannot advance {count} bytes, only {BytesRemaining} bytes remaining.");

        _position += count;
    }

    /// <summary>
    /// Releases pinned memory if necessary and resets the reader state.
    /// </summary>
    public void Dispose()
    {
        if (_pinned) _pin.Free();
        _ptr = null;
        _length = 0;
        _position = 0;
        _pinned = false;
    }
}
