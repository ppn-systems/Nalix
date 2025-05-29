using Nalix.Environment;

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
    public static sbyte ToSByte(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 1)
            throw new System.ArgumentException("Span too small to read SByte.", nameof(span));

        offset += sizeof(sbyte);
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
    public static byte ToByte(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 1)
            throw new System.ArgumentException("Span too small to read Byte.", nameof(span));

        offset += sizeof(byte);
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
    public static bool ToBool(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 1)
            throw new System.ArgumentException("Span too small to read Byte.", nameof(span));

        offset += sizeof(bool);
        return span[offset] != 0;
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
    public static unsafe ushort ToUInt16(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 2)
            throw new System.ArgumentException("Span too small to read UInt16.", nameof(span));

        offset += sizeof(ushort);
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
    public static unsafe short ToInt16(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 2)
            throw new System.ArgumentException("Span too small to read Int16.", nameof(span));

        offset += sizeof(short);
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
    public static unsafe uint ToUInt32(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to read UInt32.", nameof(span));

        offset += sizeof(uint);
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
    public static unsafe int ToInt32(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to read Int32.", nameof(span));

        offset += sizeof(int);
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
    public static unsafe ulong ToUInt64(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to read UInt64.", nameof(span));

        offset += sizeof(ulong);
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
    public static unsafe long ToInt64(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to read Int64.", nameof(span));

        offset += sizeof(long);
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
    public static unsafe float ToSingle(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to read Single (float).", nameof(span));

        offset += sizeof(float);
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
    public static unsafe double ToDouble(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to read Double.", nameof(span));

        offset += sizeof(double);
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
    public static unsafe char ToChar(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset < 0 || offset + 4 > span.Length)
            throw new System.ArgumentOutOfRangeException(nameof(offset));

        System.Span<char> charBuf = stackalloc char[1];

        int charsDecoded;
        fixed (byte* pData = &span[offset])
        fixed (char* pChar = charBuf)
        {
            charsDecoded = EncodingOptions.Encoding.GetChars(pData, 4, pChar, 1);
        }

        if (charsDecoded == 0)
            throw new System.ArgumentException("Failed to decode UTF-8 character from 4 bytes.");

        offset += 4;
        return charBuf[0];
    }

    /// <summary>
    /// Converts a <see cref="System.ReadOnlySpan{Byte}"/> to a <see cref="string"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe string ToString(this System.ReadOnlySpan<byte> span, ref int offset)
    {
        int length = span.ToInt32(ref offset); // read and update offset inside ToInt32

        if (length == -1) return null;

        if (length < 0 || offset + length > span.Length)
            throw new System.ArgumentException("Invalid string length or span too small.");

        string result = EncodingOptions.Encoding.GetString(span.Slice(offset, length));
        offset += length;
        return result;
    }

    #endregion System.ReadOnlySpan<byte> methods

    #region byte[] overloads

    /// <summary>
    /// Reads a signed byte (<see cref="sbyte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static sbyte ToSByte(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToSByte(ref offset);

    /// <summary>
    /// Reads a byte (<see cref="byte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte ToByte(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToByte(ref offset);

    /// <summary>
    /// Reads a bool (<see cref="byte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool ToBool(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToBool(ref offset);

    /// <summary>
    /// Reads a 16-bit unsigned integer (<see cref="ushort"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ushort ToUInt16(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToUInt16(ref offset);

    /// <summary>
    /// Reads a 16-bit signed integer (<see cref="short"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static short ToInt16(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToInt16(ref offset);

    /// <summary>
    /// Reads a 32-bit unsigned integer (<see cref="uint"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint ToUInt32(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToUInt32(ref offset);

    /// <summary>
    /// Reads a 32-bit signed integer (<see cref="int"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int ToInt32(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToInt32(ref offset);

    /// <summary>
    /// Reads a 64-bit unsigned integer (<see cref="ulong"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ulong ToUInt64(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToUInt64(ref offset);

    /// <summary>
    /// Reads a 64-bit signed integer (<see cref="long"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static long ToInt64(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToInt64(ref offset);

    /// <summary>
    /// Reads a 32-bit floating point number (<see cref="float"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static float ToSingle(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToSingle(ref offset);

    /// <summary>
    /// Reads a 64-bit floating point number (<see cref="double"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static double ToDouble(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToDouble(ref offset);

    /// <summary>
    /// Converts a <see cref="byte"/> array to a <see cref="char"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static char ToChar(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToChar(ref offset);

    /// <summary>
    /// Converts a <see cref="byte"/> array to a <see cref="string"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static string ToString(this byte[] buffer, ref int offset)
        => ((System.ReadOnlySpan<byte>)buffer).ToString(ref offset);

    #endregion byte[] overloads
}
