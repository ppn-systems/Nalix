namespace Nalix.Extensions.IO;

public static partial class SpanExtensions
{
    /// <summary>
    /// Writes an Array unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The Array unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteBytes(this System.Span<byte> span, byte[] value, ref int offset)
    {
        int length = value?.Length ?? -1;

        // Write length
        span.WriteInt32(length, ref offset);

        // If null or empty, return (null is -1, empty is 0 but still valid to skip copy)
        if (length <= 0)
            return;

        // Validate buffer space
        if ((uint)length > (uint)(span.Length - offset))
            throw new System.ArgumentException("Span too small to write byte array.", nameof(span));

        // Write byte array content
        fixed (byte* src = value)
        fixed (byte* dst = &System.Runtime.InteropServices.MemoryMarshal.GetReference(span[offset..]))
        {
            System.Buffer.MemoryCopy(src, dst, span.Length - offset, length);
        }

        offset += length;
    }

    /// <summary>
    /// Writes an Array unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The Array unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt16s(this System.Span<byte> span, short[] value, ref int offset)
    {
        int length = value?.Length ?? -1;
        span.WriteInt32(length, ref offset);
        if (length <= 0) return;

        int byteLength = length * sizeof(short);
        if ((uint)byteLength > (uint)(span.Length - offset))
            throw new System.ArgumentException("Span too small to write short array.", nameof(span));

        fixed (short* src = value)
        fixed (byte* dst = &System.Runtime.InteropServices.MemoryMarshal.GetReference(span.Slice(offset, byteLength)))
        {
            System.Buffer.MemoryCopy(src, dst, byteLength, byteLength);
        }

        offset += byteLength;
    }

    /// <summary>
    /// Writes an Array unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The Array unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt32s(this System.Span<byte> span, int[] value, ref int offset)
    {
        int length = value?.Length ?? -1;
        span.WriteInt32(length, ref offset);
        if (length <= 0) return;

        int byteLength = length * sizeof(int);
        if ((uint)byteLength > (uint)(span.Length - offset))
            throw new System.ArgumentException("Span too small to write int array.", nameof(span));

        fixed (int* src = value)
        fixed (byte* dst = &System.Runtime.InteropServices.MemoryMarshal.GetReference(span.Slice(offset, byteLength)))
        {
            System.Buffer.MemoryCopy(src, dst, byteLength, byteLength);
        }

        offset += byteLength;
    }
}
