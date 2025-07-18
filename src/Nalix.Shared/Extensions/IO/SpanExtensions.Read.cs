namespace Nalix.Extensions.IO;

/// <summary>
/// Provides extension methods for working with <see cref="System.Span{T}"/>, <see cref="System.ReadOnlySpan{T}"/>, and <see cref="System.Byte"/> arrays.
/// </summary>
public static partial class SpanExtensions
{
    #region System.ReadOnlySpan<byte> methods

    /// <summary>
    /// Reads a signed byte (<see cref="System.SByte"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="System.SByte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="System.SByte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.SByte ToSByte(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 1)
        {
            throw new System.ArgumentException("Span too small to read SByte.", nameof(span));
        }

        offset += sizeof(System.SByte);
        return unchecked((System.SByte)span[offset]);
    }

    /// <summary>
    /// Reads a byte (<see cref="System.Byte"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="System.Byte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="System.Byte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte ToByte(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 1)
        {
            throw new System.ArgumentException("Span too small to read Byte.", nameof(span));
        }

        offset += sizeof(System.Byte);
        return span[offset];
    }

    /// <summary>
    /// Reads a bool (<see cref="System.Byte"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The <see cref="System.Byte"/> value at the specified offset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the <see cref="System.Byte"/>.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean ToBool(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 1)
        {
            throw new System.ArgumentException("Span too small to read Byte.", nameof(span));
        }

        offset += sizeof(System.Boolean);
        return span[offset] != 0;
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer (<see cref="System.UInt16"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    /// <param name="span">The span of bytes to read from.</param>
    /// <param name="offset">The zero-based byte offset in the span to start reading.</param>
    /// <returns>The 16-bit unsigned integer read from the span.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to read the value.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.UInt16 ToUInt16(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 2)
        {
            throw new System.ArgumentException("Span too small to read UInt16.", nameof(span));
        }

        offset += sizeof(System.UInt16);
        fixed (System.Byte* ptr = &span[offset])
        {
            return *(System.UInt16*)ptr;
        }
    }

    /// <summary>
    /// Reads a 16-bit signed integer (<see cref="System.Int16"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Int16 ToInt16(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 2)
        {
            throw new System.ArgumentException("Span too small to read Int16.", nameof(span));
        }

        offset += sizeof(System.Int16);
        fixed (System.Byte* ptr = &span[offset])
        {
            return *(System.Int16*)ptr;
        }
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer (<see cref="System.UInt32"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.UInt32 ToUInt32(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 4)
        {
            throw new System.ArgumentException("Span too small to read UInt32.", nameof(span));
        }

        offset += sizeof(System.UInt32);
        fixed (System.Byte* ptr = &span[offset])
        {
            return *(System.UInt32*)ptr;
        }
    }

    /// <summary>
    /// Reads a 32-bit signed integer (<see cref="System.Int32"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Int32 ToInt32(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 4)
        {
            throw new System.ArgumentException("Span too small to read Int32.", nameof(span));
        }

        offset += sizeof(System.Int32);
        fixed (System.Byte* ptr = &span[offset])
        {
            return *(System.Int32*)ptr;
        }
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer (<see cref="System.UInt64"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.UInt64 ToUInt64(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 8)
        {
            throw new System.ArgumentException("Span too small to read UInt64.", nameof(span));
        }

        offset += sizeof(System.UInt64);
        fixed (System.Byte* ptr = &span[offset])
        {
            return *(System.UInt64*)ptr;
        }
    }

    /// <summary>
    /// Reads a 64-bit signed integer (<see cref="System.Int64"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Int64 ToInt64(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 8)
        {
            throw new System.ArgumentException("Span too small to read Int64.", nameof(span));
        }

        offset += sizeof(System.Int64);
        fixed (System.Byte* ptr = &span[offset])
        {
            return *(System.Int64*)ptr;
        }
    }

    /// <summary>
    /// Reads a 32-bit floating point number (<see cref="System.Single"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Single ToSingle(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 4)
        {
            throw new System.ArgumentException("Span too small to read Single (float).", nameof(span));
        }

        offset += sizeof(System.Single);
        fixed (System.Byte* ptr = &span[offset])
        {
            return *(System.Single*)ptr;
        }
    }

    /// <summary>
    /// Reads a 64-bit floating point number (<see cref="System.Double"/>) from a <see cref="System.ReadOnlySpan{Byte}"/> at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Double ToDouble(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (span.Length < offset + 8)
        {
            throw new System.ArgumentException("Span too small to read Double.", nameof(span));
        }

        offset += sizeof(System.Double);
        fixed (System.Byte* ptr = &span[offset])
        {
            return *(System.Double*)ptr;
        }
    }

    /// <summary>
    /// Converts a <see cref="System.ReadOnlySpan{Byte}"/> to a <see cref="System.Char"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Char ToChar(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        if (offset < 0 || offset + 4 > span.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(offset));
        }

        System.Span<System.Char> charBuf = stackalloc System.Char[1];

        System.Int32 charsDecoded;
        fixed (System.Byte* pData = &span[offset])
        fixed (System.Char* pChar = charBuf)
        {
            charsDecoded = System.Text.Encoding.UTF8.GetChars(pData, 4, pChar, 1);
        }

        if (charsDecoded == 0)
        {
            throw new System.ArgumentException("Failed to decode UTF-8 character from 4 bytes.");
        }

        offset += 4;
        return charBuf[0];
    }

    /// <summary>
    /// Converts a <see cref="System.ReadOnlySpan{Byte}"/> to a <see cref="System.String"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.String? ToString(this System.ReadOnlySpan<System.Byte> span, ref System.Int32 offset)
    {
        System.Int32 length = span.ToInt32(ref offset); // read and update offset inside ToInt32

        if (length == -1)
        {
            return null;
        }

        if (length < 0 || offset + length > span.Length)
        {
            throw new System.ArgumentException("Invalid string length or span too small.");
        }

        System.String result = System.Text.Encoding.UTF8.GetString(span.Slice(offset, length));
        offset += length;
        return result;
    }

    #endregion System.ReadOnlySpan<byte> methods

    #region byte[] overloads

    /// <summary>
    /// Reads a signed byte (<see cref="System.SByte"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.SByte ToSByte(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToSByte(ref offset);

    /// <summary>
    /// Reads a byte (<see cref="System.Byte"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte ToByte(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToByte(ref offset);

    /// <summary>
    /// Reads a bool (<see cref="System.Byte"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean ToBool(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToBool(ref offset);

    /// <summary>
    /// Reads a 16-bit unsigned integer (<see cref="System.UInt16"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 ToUInt16(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToUInt16(ref offset);

    /// <summary>
    /// Reads a 16-bit signed integer (<see cref="System.Int16"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int16 ToInt16(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToInt16(ref offset);

    /// <summary>
    /// Reads a 32-bit unsigned integer (<see cref="System.UInt32"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 ToUInt32(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToUInt32(ref offset);

    /// <summary>
    /// Reads a 32-bit signed integer (<see cref="System.Int32"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 ToInt32(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToInt32(ref offset);

    /// <summary>
    /// Reads a 64-bit unsigned integer (<see cref="System.UInt64"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt64 ToUInt64(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToUInt64(ref offset);

    /// <summary>
    /// Reads a 64-bit signed integer (<see cref="System.Int64"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int64 ToInt64(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToInt64(ref offset);

    /// <summary>
    /// Reads a 32-bit floating point number (<see cref="System.Single"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Single ToSingle(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToSingle(ref offset);

    /// <summary>
    /// Reads a 64-bit floating point number (<see cref="System.Double"/>) from a <see cref="System.Byte"/> array at the specified offset.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Double ToDouble(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToDouble(ref offset);

    /// <summary>
    /// Converts a <see cref="System.Byte"/> array to a <see cref="System.Char"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Char ToChar(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToChar(ref offset);

    /// <summary>
    /// Converts a <see cref="System.Byte"/> array to a <see cref="System.String"/> using the specified encoding.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String? ToString(this System.Byte[] buffer, ref System.Int32 offset)
        => ((System.ReadOnlySpan<System.Byte>)buffer).ToString(ref offset);

    #endregion byte[] overloads
}