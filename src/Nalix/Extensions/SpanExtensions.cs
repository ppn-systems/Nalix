using System;

namespace Nalix.Extensions;

/// <summary>
/// Provides extension methods for working with <see cref="Span{T}"/>, <see cref="ReadOnlySpan{T}"/>, and <see cref="byte"/> arrays.
/// </summary>
public static class SpanExtensions
{
    #region ReadOnlySpan<byte> methods

    /// <summary>
    /// Reads a signed byte (<see cref="sbyte"/>) from a <see cref="ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="sbyte"/> value at the specified offset.</returns>
    /// <exception cref="ArgumentException">Thrown when the span is too small to read the <see cref="sbyte"/>.</exception>
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
    public static unsafe double ToDouble(this ReadOnlySpan<byte> span, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new ArgumentException("Span too small to read Double.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            return *(double*)ptr;
        }
    }

    #endregion ReadOnlySpan<byte> methods

    #region byte[] overloads

    /// <summary>
    /// Reads a signed byte (<see cref="sbyte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static sbyte ToSByte(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToSByte(offset);

    /// <summary>
    /// Reads a byte (<see cref="byte"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static byte ToByte(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToByte(offset);

    /// <summary>
    /// Reads a 16-bit unsigned integer (<see cref="ushort"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static ushort ToUInt16(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToUInt16(offset);

    /// <summary>
    /// Reads a 16-bit signed integer (<see cref="short"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static short ToInt16(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToInt16(offset);

    /// <summary>
    /// Reads a 32-bit unsigned integer (<see cref="uint"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static uint ToUInt32(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToUInt32(offset);

    /// <summary>
    /// Reads a 32-bit signed integer (<see cref="int"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static int ToInt32(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToInt32(offset);

    /// <summary>
    /// Reads a 64-bit unsigned integer (<see cref="ulong"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static ulong ToUInt64(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToUInt64(offset);

    /// <summary>
    /// Reads a 64-bit signed integer (<see cref="long"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static long ToInt64(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToInt64(offset);

    /// <summary>
    /// Reads a 32-bit floating point number (<see cref="float"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static float ToSingle(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToSingle(offset);

    /// <summary>
    /// Reads a 64-bit floating point number (<see cref="double"/>) from a <see cref="byte"/> array at the specified offset.
    /// </summary>
    public static double ToDouble(this byte[] buffer, int offset = 0)
        => ((ReadOnlySpan<byte>)buffer).ToDouble(offset);

    #endregion byte[] overloads
}
