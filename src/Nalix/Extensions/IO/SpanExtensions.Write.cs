using Nalix.Serialization;

namespace Nalix.Extensions.IO;

/// <summary>
/// Provides extension methods for writing various data types to a <see cref="System.Span{T}"/> or byte array.
/// </summary>
public static partial class SpanExtensions
{
    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteSByte(this System.Span<byte> span, sbyte value, int offset = 0)
        => span[offset] = unchecked((byte)value);

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteSByte(this byte[] array, sbyte value, int offset = 0)
        => array[offset] = unchecked((byte)value);

    /// <summary>
    /// Writes an 8-bit unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 8-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this System.Span<byte> span, byte value, int offset = 0) => span[offset] = value;

    /// <summary>
    /// Writes an 8-bit unsigned integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this byte[] array, byte value, int offset = 0)
    {
        array[offset] = value;
    }

    /// <summary>
    /// Writes a 16-bit signed integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 16-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to write a 16-bit integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt16(this System.Span<byte> span, short value, int offset = 0)
    {
        if (span.Length < offset + 2)
            throw new System.ArgumentException("Span too small to write Int16.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(short*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 16-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 16-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the array is too small to write a 16-bit integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt16(this byte[] array, short value, int offset = 0)
    {
        if (array.Length < offset + 2)
            throw new System.ArgumentException("Array too small to write Int16.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(short*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 16-bit unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 16-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to write a 16-bit unsigned integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteUInt16(this System.Span<byte> span, ushort value, int offset = 0)
    {
        if (span.Length < offset + 2)
            throw new System.ArgumentException("Span too small to write UInt16.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(ushort*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 16-bit unsigned integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 16-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the array is too small to write a 16-bit unsigned integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteUInt16(this byte[] array, ushort value, int offset = 0)
    {
        if (array.Length < offset + 2)
            throw new System.ArgumentException("Array too small to write UInt16.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(ushort*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 32-bit signed integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 32-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to write a 32-bit integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt32(this System.Span<byte> span, int value, int offset = 0)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to write Int32.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(int*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 32-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 32-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the array is too small to write a 32-bit integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt32(this byte[] array, int value, int offset = 0)
    {
        if (array.Length < offset + 4)
            throw new System.ArgumentException("Array too small to write Int32.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(int*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 32-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to write a 32-bit unsigned integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteUInt32(this System.Span<byte> span, uint value, int offset = 0)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to write UInt32.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(uint*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 32-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the array is too small to write a 32-bit unsigned integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteUInt32(this byte[] array, uint value, int offset = 0)
    {
        if (array.Length < offset + 4)
            throw new System.ArgumentException("Array too small to write UInt32.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(uint*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 64-bit signed integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 64-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to write a 64-bit integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt64(this System.Span<byte> span, long value, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to write Int64.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(long*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 64-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 64-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the array is too small to write a 64-bit integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt64(this byte[] array, long value, int offset = 0)
    {
        if (array.Length < offset + 8)
            throw new System.ArgumentException("Array too small to write Int64.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(long*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 64-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to write a 64-bit unsigned integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteUInt64(this System.Span<byte> span, ulong value, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to write UInt64.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(ulong*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 64-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the array is too small to write a 64-bit unsigned integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteUInt64(this byte[] array, ulong value, int offset = 0)
    {
        if (array.Length < offset + 8)
            throw new System.ArgumentException("Array too small to write UInt64.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(ulong*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a single-precision floating-point number to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The single-precision floating-point number to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to write a single-precision float.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteSingle(this System.Span<byte> span, float value, int offset = 0)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to write Single.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(float*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a single-precision floating-point number to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The single-precision floating-point number to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the array is too small to write a single-precision float.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteSingle(this byte[] array, float value, int offset = 0)
    {
        if (array.Length < offset + 4)
            throw new System.ArgumentException("Array too small to write Single.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(float*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a double-precision floating-point number to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The double-precision floating-point number to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to write a double-precision float.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteDouble(this System.Span<byte> span, double value, int offset = 0)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to write Double.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(double*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a double-precision floating-point number to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The double-precision floating-point number to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the array is too small to write a double-precision float.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteDouble(this byte[] array, double value, int offset = 0)
    {
        if (array.Length < offset + 8)
            throw new System.ArgumentException("Array too small to write Double.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(double*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a character to the specified offset in the span.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteChar(this System.Span<byte> span, char value, int offset = 0)
    {
        if (span.Length < offset + 2)
            throw new System.ArgumentException("Span too small to write Char.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(char*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a character to the specified offset in the byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteChar(this byte[] buffer, char value, int offset = 0)
    {
        if (buffer.Length < offset + 2)
            throw new System.ArgumentException("Buffer too small to write Char.", nameof(buffer));

        fixed (byte* ptr = &buffer[offset])
        {
            *(char*)ptr = value;
        }
    }

    /// <summary>
    /// Writes a character to the specified offset in the byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe int WriteString(this System.Span<byte> span, string value, int offset = 0, System.Text.Encoding encoding = null)
    {
        System.ArgumentNullException.ThrowIfNull(value);

        encoding ??= JsonOptions.Encoding;

        // Calculate byte count for the encoded string
        int byteCount = encoding.GetByteCount(value);

        if (span.Length < offset + byteCount)
            throw new System.ArgumentException("Span too small to write string.", nameof(span));

        fixed (char* pChars = value)
        fixed (byte* pBytes = &span[offset])
        {
            // Encode chars to bytes directly into span's memory
            return encoding.GetBytes(pChars, value.Length, pBytes, span.Length - offset);
        }
    }

    /// <summary>
    /// Writes a string to the specified offset in the byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe int WriteString(this byte[] buffer, string value, int offset = 0, System.Text.Encoding encoding = null)
    {
        System.ArgumentNullException.ThrowIfNull(value);

        encoding ??= JsonOptions.Encoding;

        int byteCount = encoding.GetByteCount(value);
        if (buffer.Length < offset + byteCount)
            throw new System.ArgumentException("Buffer too small to write string.", nameof(buffer));

        fixed (char* pChars = value)
        fixed (byte* pBytes = &buffer[offset])
        {
            return encoding.GetBytes(pChars, value.Length, pBytes, buffer.Length - offset);
        }
    }
}
