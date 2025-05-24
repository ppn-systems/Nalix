using Nalix.Serialization.Internal.Writers;

namespace Nalix.Serialization;

/// <summary>
/// Provides functionality for writing serialized data with an internal buffer.
/// </summary>
/// <param name="initialSize">The initial size of the buffer segment.</param>
public partial struct SerializationWriter(int initialSize)
{
    /// <summary>
    /// The internal buffer segment used for serialization.
    /// </summary>
    internal BufferSegment Segment = new(initialSize);

    /// <summary>
    /// Advances the current position in the buffer by the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to advance.</param>
    public void Advance(int count) => Segment.Advance(count);

    /// <summary>
    /// Retrieves a span of free buffer space with the specified length.
    /// </summary>
    /// <param name="length">The requested length of the span.</param>
    /// <returns>A span of bytes representing the available buffer space.</returns>
    public readonly System.Span<byte> GetSpan(int length) => Segment.FreeBuffer[..length];
}
