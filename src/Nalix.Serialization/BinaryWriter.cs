using Nalix.Serialization.Internal;

namespace Nalix.Serialization;

/// <summary>
/// Provides functionality for writing serialized data with an internal buffer.
/// </summary>
/// <param name="initialSize">The initial size of the buffer segment.</param>
public partial struct BinaryWriter(int initialSize)
{
    private BufferSegment _segment = new(initialSize);

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
    public void Advance(int count) => _segment.AdvanceWrite(count);

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
    public readonly System.Span<byte> GetWrittenBuffer() => _segment.WrittenBuffer;
}
