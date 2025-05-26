using Nalix.Serialization.Internal;

namespace Nalix.Serialization.Buffers;

/// <summary>
/// Provides functionality for writing serialized data with an internal buffer.
/// </summary>
public struct DataWriter
{
    private BufferSegment _segment;

    /// <summary>
    /// Gets the current buffer segment used for writing data.
    /// </summary>
    public readonly int BytesWritten => _segment.WrittenCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataWriter"/> struct with the specified initial buffer size.
    /// </summary>
    /// <param name="initialSize">The initial size of the buffer.</param>
    public DataWriter(int initialSize) => _segment = new BufferSegment(initialSize);

    /// <summary>
    /// Initializes a new instance of the <see cref="DataWriter"/> struct with the specified byte array.
    /// </summary>
    /// <param name="bytes">The byte array to initialize the buffer with.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the provided byte array is null or empty.</exception>
    public DataWriter(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            throw new System.ArgumentNullException(nameof(bytes), "Buffer cannot be null or empty.");
        }
        _segment = new BufferSegment(bytes);
    }

    /// <summary>
    /// Xoá bộ nhớ đệm, trả lại ArrayPool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Clear() => _segment.Clear();

    /// <summary>
    /// Advances the current position in the buffer by the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to advance.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => _segment.Advance(count);

    /// <summary>
    /// Expands the buffer to accommodate additional data.
    /// </summary>
    /// <param name="count">The number of bytes to expand.</param>
    public void Expand(int count) => _segment.Expand(count);

    /// <summary>
    /// Retrieves a span of free buffer space with the specified length.
    /// </summary>
    /// <param name="length">The requested length of the span.</param>
    /// <returns>A span of bytes representing the available buffer space.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly System.Span<byte> GetSpan(int length) => _segment.FreeBuffer[..length];

    /// <summary>
    /// Retrieves a reference to the first byte in the free buffer space with the specified size hint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly byte[] ToArray() => _segment.WrittenBuffer.ToArray();
}
