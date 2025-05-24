using Nalix.Serialization.Internal;

namespace Nalix.Serialization;

/// <summary>
/// Provides functionality for reading serialized data from an internal buffer.
/// </summary>
public struct SerializationReader(int initialSize)
{
    internal BufferSegment Segment = new(initialSize);

    /// <summary>
    /// Xoá bộ nhớ đệm, trả lại ArrayPool.
    /// </summary>
    public void Clear() => Segment.Clear();

    /// <summary>
    /// Advances the current position in the buffer by the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to advance.</param>
    public void Advance(int count) => Segment.AdvanceRead(count);

    /// <summary>
    /// Retrieves a span of free buffer space with the specified length.
    /// </summary>
    /// <param name="length">The requested length of the span.</param>
    /// <returns>A span of bytes representing the available buffer space.</returns>
    public readonly System.Span<byte> GetSpan(int length) => Segment.ReadBuffer[..length];
}
