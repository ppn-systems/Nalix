using Nalix.Serialization;

namespace Nalix.Extensions.IO;

/// <summary>
/// Provides extension methods for working with <see cref="System.Span{T}"/>, <see cref="System.ReadOnlySpan{T}"/>, and <see cref="byte"/> arrays.
/// </summary>
public static partial class SpanExtensions
{
    #region System.ReadOnlySpan<byte> methods

    /// <summary>
    /// Reads a signed byte (<see cref="sbyte"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="sbyte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="sbyte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static sbyte ToSByte(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 1)
            throw new System.ArgumentException("Span too small to read SByte.", nameof(span));

        return unchecked((sbyte)span[offset]);
    }

    /// <summary>
    /// Reads a byte (<see cref="byte"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="byte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="byte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte ToByte(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 1)
            throw new System.ArgumentException("Span too small to read Byte.", nameof(span));

        return span[offset];
    }

    /// <summary>
    /// Reads a bool (<see cref="byte"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="byte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="byte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool ToBool(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 1)
            throw new System.ArgumentException("Span too small to read Byte.", nameof(span));

        return span[offset] != 0;
    }

    /// <summary>
    /// Reads a byte[] (<see cref="byte"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="byte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="byte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe byte[] ReadBytes(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        int length = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<int>(ref
            System.Runtime.CompilerServices.Unsafe.Add(ref
            System.Runtime.InteropServices.MemoryMarshal.GetReference(span), offset));
        offset += 4;

        if (length == 0)
            return [];

        byte[] result = new byte[length];

        fixed (byte* src = &System.Runtime.InteropServices.MemoryMarshal.GetReference(span.Slice(offset, length)))
        fixed (byte* dst = result)
        {
            System.Buffer.MemoryCopy(src, dst, length, length);
        }

        offset += length;
        return result;
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer (<see cref="ushort"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The 16-bit unsigned integer read from the span.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the value.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe ushort ToUInt16(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 2)
            throw new System.ArgumentException("Span too small to read UInt16.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(ushort*)ptr;
        }
    }

    /// <summary>
    /// Reads a 16-bit signed integer (<see cref="short"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe short ToInt16(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 2)
            throw new System.ArgumentException("Span too small to read Int16.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(short*)ptr;
        }
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer (<see cref="uint"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe uint ToUInt32(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to read UInt32.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(uint*)ptr;
        }
    }

    /// <summary>
    /// Reads a 32-bit signed integer (<see cref="int"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe int ToInt32(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to read Int32.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(int*)ptr;
        }
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer (<see cref="ulong"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe ulong ToUInt64(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to read UInt64.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(ulong*)ptr;
        }
    }

    /// <summary>
    /// Reads a 64-bit signed integer (<see cref="long"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe long ToInt64(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to read Int64.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(long*)ptr;
        }
    }

    /// <summary>
    /// Reads a 32-bit floating point number (<see cref="float"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe float ToSingle(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to read Single (float).", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(float*)ptr;
        }
    }

    /// <summary>
    /// Reads a 64-bit floating point number (<see cref="double"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe double ToDouble(this System.ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to read Double.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(double*)ptr;
        }
    }

    /// <summary>
    /// Converts a <see cref="System.ReadOnlySpan{Byte}"/> to a <see cref="char"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe char ToChar(this System.ReadOnlySpan<byte> span, int offset = 0, System.Text.Encoding encoding = null)
    {
        encoding ??= SerializationOptions.Encoding;

        if (offset < 0 || offset >= span.Length)
            throw new System.ArgumentOutOfRangeException(nameof(offset));

        fixed (byte* pBytes = &span[offset])
        {
            // Decode one char from bytes starting at offset
            // Since UTF-8 char can be multiple bytes, decode 4 bytes max (max UTF-8 char length)
            System.Span<byte> buffer = stackalloc byte[System.Math.Min(4, span.Length - offset)];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = span[offset + i];

            System.Span<char> chars = stackalloc char[1];
            int charsDecoded = encoding.GetChars(buffer, chars);
            if (charsDecoded == 0)
                throw new System.ArgumentException("Invalid bytes for decoding char.");

            return chars[0];
        }
    }

    /// <summary>
    /// Converts a <see cref="System.ReadOnlySpan{Byte}"/> to a <see cref="string"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe string ToString(
        this System.ReadOnlySpan<byte> span,
        int offset = 0, int length = -1,
        System.Text.Encoding encoding = null)
    {
        encoding ??= SerializationOptions.Encoding
            ?? throw new System.ArgumentNullException(nameof(encoding));

        if (length < 0)
            length = span.Length - offset;

        if (offset < 0 || length < 0 || offset + length > span.Length)
            throw new System.ArgumentOutOfRangeException(nameof(length), "Invalid offset and length.");

        fixed (byte* pBytes = &span[offset])
        {
            // Decode bytes to string using pointer overload
            return encoding.GetString(pBytes, length);
        }
    }

    #endregion System.ReadOnlySpan<byte> methods

    #region byte[] overloads

    /// <summary>
    /// Reads a signed byte (<see cref="sbyte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static sbyte ToSByte(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToSByte(offset);

    /// <summary>
    /// Reads a byte (<see cref="byte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte ToByte(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToByte(offset);

    /// <summary>
    /// Reads a bool (<see cref="byte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool ToBool(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToBool(offset);

    /// <summary>
    /// Reads a 16-bit unsigned integer (<see cref="ushort"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ushort ToUInt16(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToUInt16(offset);

    /// <summary>
    /// Reads a 16-bit signed integer (<see cref="short"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static short ToInt16(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToInt16(offset);

    /// <summary>
    /// Reads a 32-bit unsigned integer (<see cref="uint"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint ToUInt32(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToUInt32(offset);

    /// <summary>
    /// Reads a 32-bit signed integer (<see cref="int"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int ToInt32(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToInt32(offset);

    /// <summary>
    /// Reads a 64-bit unsigned integer (<see cref="ulong"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ulong ToUInt64(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToUInt64(offset);

    /// <summary>
    /// Reads a 64-bit signed integer (<see cref="long"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static long ToInt64(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToInt64(offset);

    /// <summary>
    /// Reads a 32-bit floating point number (<see cref="float"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static float ToSingle(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToSingle(offset);

    /// <summary>
    /// Reads a 64-bit floating point number (<see cref="double"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static double ToDouble(this byte[] buffer, int offset = 0)
        => ((System.ReadOnlySpan<byte>)buffer).ToDouble(offset);

    /// <summary>
    /// Converts a <see cref="byte"/> array to a <see cref="char"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static char ToChar(this byte[] buffer, int offset = 0, System.Text.Encoding encoding = null)
        => ((System.ReadOnlySpan<byte>)buffer).ToChar(offset, encoding);

    /// <summary>
    /// Converts a <see cref="byte"/> array to a <see cref="string"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static string ToString(
        this byte[] buffer, int offset = 0, int length = -1, System.Text.Encoding encoding = null)
        => ((System.ReadOnlySpan<byte>)buffer).ToString(offset, length, encoding);

    #endregion byte[] overloads
}
