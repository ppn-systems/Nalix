using System;

namespace Nalix.Extensions.IO;

public static partial class SpanExtensions
{
    /// <summary>
    /// Writes an unmanaged value of type <typeparamref name="T"/> to the specified <see cref="Span{T}"/> at the given offset.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to write.</typeparam>
    /// <param name="span">The span of bytes to write to.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="offset">The offset in the span where writing begins. This value is incremented by the size of <typeparamref name="T"/>.</param>
    public static unsafe void Write<T>(this Span<byte> span, T value, ref int offset) where T : unmanaged
    {
        fixed (byte* ptr = span[offset..])
        {
            *(T*)ptr = value;
        }
        offset += sizeof(T);
    }

    /// <summary>
    /// Reads an unmanaged value of type <typeparamref name="T"/> from the specified <see cref="ReadOnlySpan{T}"/> at the given offset.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to read.</typeparam>
    /// <param name="span">The read-only span of bytes to read from.</param>
    /// <param name="offset">The offset in the span where reading begins. This value is incremented by the size of <typeparamref name="T"/>.</param>
    /// <returns>The value read from the span.</returns>
    public static unsafe T Read<T>(this ReadOnlySpan<byte> span, ref int offset) where T : unmanaged
    {
        T value;
        fixed (byte* ptr = span[offset..])
        {
            value = *(T*)ptr;
        }
        offset += sizeof(T);
        return value;
    }
}
