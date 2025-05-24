using Nalix.Serialization.Internal;

namespace Nalix.Serialization;

/// <summary>
/// Provides functionality for reading serialized data from an internal buffer.
/// </summary>
public struct SerializationReader(int initialSize)
{
    private BufferSegment _segment = new(initialSize);

    /// <summary>
    /// Xoá bộ nhớ đệm, trả lại ArrayPool.
    /// </summary>
    public void Clear() => _segment.Clear();

    /// <summary>
    /// Advances the current position in the buffer by the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to advance.</param>
    public void Advance(int count) => _segment.AdvanceRead(count);

    /// <summary>
    /// Retrieves a span of free buffer space with the specified length.
    /// </summary>
    /// <param name="length">The requested length of the span.</param>
    /// <returns>A span of bytes representing the available buffer space.</returns>
    public readonly System.ReadOnlySpan<byte> GetSpan(int length) => _segment.ReadBuffer[..length];
}
