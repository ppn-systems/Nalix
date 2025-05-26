using Nalix.Serialization.Internal;

namespace Nalix.Serialization.Buffers;

/// <summary>
/// Provides functionality for writing serialized data with an internal buffer.
/// </summary>
public struct DataWriter
{
    private BufferSegment _segment;

    internal readonly int BytesWritten => _segment.WrittenCount;

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
    /// Retrieves a span of free buffer space with the specified length.
    /// </summary>
    /// <param name="length">The requested length of the span.</param>
    /// <returns>A span of bytes representing the available buffer space.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly System.Span<byte> GetSpan(int length) => _segment.FreeBuffer[..length];

    /// <summary>
    /// Retrieves the portion of the buffer that has been written to.
    /// </summary>
    /// <returns>A span of bytes representing the written data in the buffer.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly System.Span<byte> ToArray() => _segment.WrittenBuffer;
}
