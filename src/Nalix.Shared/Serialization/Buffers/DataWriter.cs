using Nalix.Shared.Serialization.Internal;

namespace Nalix.Shared.Serialization.Buffers;

/// <summary>
/// Provides functionality for writing serialized data with an internal buffer.
/// </summary>
public struct DataWriter : System.IDisposable
{
    private BufferSegment _segment;

    /// <summary>
    /// Gets the current buffer segment used for writing data.
    /// </summary>
    public readonly System.Int32 BytesWritten => _segment.WrittenCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataWriter"/> struct with the specified initial buffer size.
    /// </summary>
    /// <param name="initialSize">The initial size of the buffer.</param>
    public DataWriter(System.Int32 initialSize) => _segment = new BufferSegment(initialSize);

    /// <summary>
    /// Initializes a new instance of the <see cref="DataWriter"/> struct with the specified byte array.
    /// </summary>
    /// <param name="bytes">The byte array to initialize the buffer with.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the provided byte array is null or empty.</exception>
    public DataWriter(System.Byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            throw new System.ArgumentNullException(nameof(bytes), "Buffer cannot be null or empty.");
        }
        _segment = new BufferSegment(bytes);
    }

    /// <summary>
    /// Advances the current position in the buffer by the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to advance.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Advance(System.Int32 count)
    {
        _segment.Advance(count);
#if DEBUG
        System.Console.WriteLine($"[DEBUG] Writer advanced by {count} → total {_segment.WrittenCount} bytes");
#endif
    }

    /// <summary>
    /// Expands the buffer to accommodate additional data.
    /// </summary>
    /// <param name="count">The number of bytes to expand.</param>
    public void Expand(System.Int32 count) => _segment.Expand(count);

    /// <summary>
    /// Retrieves a span of free buffer space with the specified length.
    /// </summary>
    /// <param name="length">The requested length of the span.</param>
    /// <returns>A span of bytes representing the available buffer space.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly System.Span<System.Byte> GetSpan(System.Int32 length) => _segment.FreeBuffer[..length];

    /// <summary>
    /// Retrieves a reference to the first byte in the free buffer space with the specified size hint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly byte[] ToArray()
    {
        if (_segment.WrittenCount == 0)
            return [];

        return _segment.WrittenBuffer[.._segment.WrittenCount].ToArray();
    }

    /// <summary>
    /// Xoá bộ nhớ đệm, trả lại ArrayPool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _segment.Clear();
}
