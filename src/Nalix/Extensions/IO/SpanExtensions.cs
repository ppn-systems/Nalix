using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Nalix.Extensions.IO;

/// <summary>
/// Provides extension methods for working with <see cref="Span{T}"/>, <see cref="ReadOnlySpan{T}"/>, and <see cref="byte"/> arrays.
/// </summary>
public static partial class SpanExtensions
{
    #region ReadOnlySpan<byte> methods

    /// <summary>
    /// Reads a signed byte (<see cref="sbyte"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="sbyte"/> value at the specified offset.</returns>
    /// <exception cref="ArgumentException">Thrown when the span is too small to read the <see cref="sbyte"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte ToSByte(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 1)
            throw new ArgumentException("Span too small to read SByte.", nameof(span));

        return unchecked((sbyte)span[offset]);
    }

    /// <summary>
    /// Reads a byte (<see cref="byte"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="byte"/> value at the specified offset.</returns>
    /// <exception cref="ArgumentException">Thrown when the span is too small to read the <see cref="byte"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToByte(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 1)
            throw new ArgumentException("Span too small to read Byte.", nameof(span));

        return span[offset];
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer (<see cref="ushort"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The 16-bit unsigned integer read from the span.</returns>
    /// <exception cref="ArgumentException">Thrown when the span is too small to read the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ushort ToUInt16(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 2)
            throw new ArgumentException("Span too small to read UInt16.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(ushort*)ptr;
        }
    }

    /// <summary>
    /// Reads a 16-bit signed integer (<see cref="short"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe short ToInt16(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 2)
            throw new ArgumentException("Span too small to read Int16.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(short*)ptr;
        }
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer (<see cref="uint"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint ToUInt32(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 4)
            throw new ArgumentException("Span too small to read UInt32.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(uint*)ptr;
        }
    }

    /// <summary>
    /// Reads a 32-bit signed integer (<see cref="int"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int ToInt32(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 4)
            throw new ArgumentException("Span too small to read Int32.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(int*)ptr;
        }
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer (<see cref="ulong"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ulong ToUInt64(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new ArgumentException("Span too small to read UInt64.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(ulong*)ptr;
        }
    }

    /// <summary>
    /// Reads a 64-bit signed integer (<see cref="long"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe long ToInt64(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new ArgumentException("Span too small to read Int64.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(long*)ptr;
        }
    }

    /// <summary>
    /// Reads a 32-bit floating point number (<see cref="float"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe float ToSingle(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 4)
            throw new ArgumentException("Span too small to read Single (float).", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(float*)ptr;
        }
    }

    /// <summary>
    /// Reads a 64-bit floating point number (<see cref="double"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe double ToDouble(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new ArgumentException("Span too small to read Double.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(double*)ptr;
        }
    }

    /// <summary>
    /// Converts a <see cref="ReadOnlySpan{Byte}"/> to a <see cref="char"/> using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe char ToChar(this ReadOnlySpan<byte> span, int offset = 0, Encoding encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (offset < 0 || offset >= span.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        fixed (byte* pBytes = &span[offset])
        {
            // Decode one char from bytes starting at offset
            // Since UTF-8 char can be multiple bytes, decode 4 bytes max (max UTF-8 char length)
            Span<byte> buffer = stackalloc byte[System.Math.Min(4, span.Length - offset)];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = span[offset + i];

            Span<char> chars = stackalloc char[1];
            int charsDecoded = encoding.GetChars(buffer, chars);
            if (charsDecoded == 0)
                throw new ArgumentException("Invalid bytes for decoding char.");

            return chars[0];
        }
    }

    /// <summary>
    /// Converts a <see cref="ReadOnlySpan{Byte}"/> to a <see cref="string"/> using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe string ToString(
        this ReadOnlySpan<byte> span, int offset = 0, int length = -1, Encoding encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (length < 0)
            length = span.Length - offset;

        if (offset < 0 || length < 0 || offset + length > span.Length)
            throw new ArgumentOutOfRangeException(nameof(length), "Invalid offset and length.");

        fixed (byte* pBytes = &span[offset])
        {
            // Decode bytes to string using pointer overload
            return encoding.GetString(pBytes, length);
        }
    }

    #endregion ReadOnlySpan<byte> methods

    #region byte[] overloads

    /// <summary>
    /// Reads a signed byte (<see cref="sbyte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte ToSByte(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToSByte(offset);

    /// <summary>
    /// Reads a byte (<see cref="byte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToByte(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToByte(offset);

    /// <summary>
    /// Reads a 16-bit unsigned integer (<see cref="ushort"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ToUInt16(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToUInt16(offset);

    /// <summary>
    /// Reads a 16-bit signed integer (<see cref="short"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ToInt16(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToInt16(offset);

    /// <summary>
    /// Reads a 32-bit unsigned integer (<see cref="uint"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToUInt32(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToUInt32(offset);

    /// <summary>
    /// Reads a 32-bit signed integer (<see cref="int"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToInt32(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToInt32(offset);

    /// <summary>
    /// Reads a 64-bit unsigned integer (<see cref="ulong"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToUInt64(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToUInt64(offset);

    /// <summary>
    /// Reads a 64-bit signed integer (<see cref="long"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToInt64(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToInt64(offset);

    /// <summary>
    /// Reads a 32-bit floating point number (<see cref="float"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToSingle(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToSingle(offset);

    /// <summary>
    /// Reads a 64-bit floating point number (<see cref="double"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToDouble(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToDouble(offset);

    /// <summary>
    /// Converts a <see cref="byte"/> array to a <see cref="char"/> using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char ToChar(this byte[] buffer, int offset = 0, Encoding encoding = null)
        => ((ReadOnlySpan<byte>)buffer).ToChar(offset, encoding);

    /// <summary>
    /// Converts a <see cref="byte"/> array to a <see cref="string"/> using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToString(
        this byte[] buffer, int offset = 0, int length = -1, Encoding encoding = null)
        => ((ReadOnlySpan<byte>)buffer).ToString(offset, length, encoding);

    #endregion byte[] overloads
}
