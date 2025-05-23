namespace Nalix.Extensions.IO;

public static partial class SpanExtensions
{
    /// <summary>
    /// Reads a byte[] (<see cref="byte"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="byte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="byte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe byte[] ToBytes(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        int length = span.ToInt32(ref offset);

        if (length == -1)
            return null; // Properly return null, not empty array

        if (length < 0 || offset + length > span.Length)
            throw new System.ArgumentException("Invalid or corrupt byte array length.");

        byte[] result = System.GC.AllocateUninitializedArray<byte>(length);

        fixed (byte* src = &System.Runtime.InteropServices.MemoryMarshal.GetReference(span.Slice(offset, length)))
        fixed (byte* dst = result)
        {
            System.Buffer.MemoryCopy(src, dst, length, length);
        }

        offset += length;
        return result;
    }

    /// <summary>
    /// Reads a short[] (<see cref="short"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="byte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="byte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe short[] ToInt16s(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        int length = span.ToInt32(ref offset);

        if (length == -1)
            return null;

        if (length < 0 || (uint)(length * sizeof(short)) > (uint)(span.Length - offset))
            throw new System.ArgumentException("Invalid or corrupt short array length.");

        short[] result = System.GC.AllocateUninitializedArray<short>(length);

        fixed (byte* src = &System.Runtime.InteropServices.MemoryMarshal.GetReference(span.Slice(offset, length * sizeof(short))))
        fixed (short* dst = result)
        {
            System.Buffer.MemoryCopy(src, dst, length * sizeof(short), length * sizeof(short));
        }
        offset += length * sizeof(short);
        return result;
    }

    /// <summary>
    /// Reads a int[] (<see cref="int"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="byte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="byte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe int[] ToInt32s(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        int length = span.ToInt32(ref offset);

        if (length == -1)
            return null;

        if (length < 0 || (uint)(length * sizeof(int)) > (uint)(span.Length - offset))
            throw new System.ArgumentException("Invalid or corrupt int array length.");

        int[] result = new int[length];
        fixed (byte* src = &System.Runtime.InteropServices.MemoryMarshal.GetReference(span.Slice(offset, length * sizeof(int))))
        fixed (int* dst = result)
        {
            System.Buffer.MemoryCopy(src, dst, length * sizeof(int), length * sizeof(int));
        }
        offset += length * sizeof(int);
        return result;
    }
}
