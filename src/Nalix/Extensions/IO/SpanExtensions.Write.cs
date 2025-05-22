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
    public static void WriteSByte(this System.Span<byte> span, in sbyte value, int offset = 0)
        => span[offset] = unchecked((byte)value);

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteSByte(this byte[] array, in sbyte value, int offset = 0)
        => array[offset] = unchecked((byte)value);

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteBool(this System.Span<byte> span, in bool value, int offset = 0)
        => span[offset] = value ? (byte)1 : (byte)0;

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteBool(this byte[] array, in bool value, int offset = 0)
        => array[offset] = value ? (byte)1 : (byte)0;

    /// <summary>
    /// Writes an 8-bit unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 8-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this System.Span<byte> span, in byte value, int offset = 0) => span[offset] = value;

    /// <summary>
    /// Writes an 8-bit unsigned integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this byte[] array, in byte value, int offset = 0)
    {
        array[offset] = value;
    }

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
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
            ref System.Runtime.CompilerServices.Unsafe.Add(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(span), offset), length);
        offset += sizeof(int);

        // If null or empty, return (null is -1, empty is 0 but still valid to skip copy)
        if (length <= 0)
            return;

        // Validate buffer space
        if (span.Length < offset + length)
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
    /// Writes a 16-bit signed integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 16-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    /// <exception cref="System.ArgumentException">Thrown when the span is too small to write a 16-bit integer.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt16(this System.Span<byte> span, in short value, int offset = 0)
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
    public static unsafe void WriteInt16(this byte[] array, in short value, int offset = 0)
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
    public static unsafe void WriteUInt16(this System.Span<byte> span, in ushort value, int offset = 0)
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
    public static unsafe void WriteUInt16(this byte[] array, in ushort value, int offset = 0)
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
    public static unsafe void WriteInt32(this System.Span<byte> span, in int value, int offset = 0)
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
    public static unsafe void WriteInt32(this byte[] array, in int value, int offset = 0)
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
    public static unsafe void WriteUInt32(this System.Span<byte> span, in uint value, int offset = 0)
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
    public static unsafe void WriteUInt32(this byte[] array, in uint value, int offset = 0)
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
    public static unsafe void WriteInt64(this System.Span<byte> span, in long value, int offset = 0)
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
    public static unsafe void WriteInt64(this byte[] array, in long value, int offset = 0)
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
    public static unsafe void WriteUInt64(this System.Span<byte> span, in ulong value, int offset = 0)
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
    public static unsafe void WriteUInt64(this byte[] array, in ulong value, int offset = 0)
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
    public static unsafe void WriteSingle(this System.Span<byte> span, in float value, int offset = 0)
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
    public static unsafe void WriteSingle(this byte[] array, in float value, int offset = 0)
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
    public static unsafe void WriteDouble(this System.Span<byte> span, in double value, int offset = 0)
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
    public static unsafe void WriteDouble(this byte[] array, in double value, int offset = 0)
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
    public static unsafe void WriteChar(this System.Span<byte> span, in char value, ref int offset)
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
    public static void WriteChar(
        this byte[] buffer,
        in char value, ref int offset,
        System.Text.Encoding encoding = null)
    {
        encoding ??= SerializationOptions.Encoding ?? System.Text.Encoding.UTF8;

        int maxByteCount = encoding.GetMaxByteCount(1);
        if (buffer.Length < offset + maxByteCount)
            throw new System.ArgumentException("Buffer too small to write Char.", nameof(buffer));

        System.Span<byte> tempSpan = stackalloc byte[maxByteCount];
        System.ReadOnlySpan<char> charSpan = System.Runtime.InteropServices.MemoryMarshal
            .CreateReadOnlySpan(ref System.Runtime.CompilerServices.Unsafe.AsRef(in value), 1);

        int bytesWritten = encoding.GetBytes(charSpan, tempSpan);
        tempSpan[..bytesWritten].CopyTo(System.MemoryExtensions.AsSpan(buffer, offset));

        offset += bytesWritten;
    }

    /// <summary>
    /// Writes a character to the specified offset in the byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteString(
        this System.Span<byte> span,
        string value, ref int offset,
        System.Text.Encoding encoding = null)
    {
        if (value == null)
        {
            span.WriteInt32(-1, offset);
            offset += sizeof(int);
            return;
        }

        encoding ??= SerializationOptions.Encoding ??
            throw new System.ArgumentNullException(nameof(encoding));

        int byteCount = encoding.GetByteCount(value);

        if (span.Length < offset + sizeof(int) + byteCount)
            throw new System.ArgumentException("Span too small to write string.", nameof(span));

        // Write string length
        span.WriteInt32(byteCount, offset);
        offset += sizeof(int);

        fixed (char* pChars = value)
        fixed (byte* pBytes = &span[offset])
        {
            int written = encoding.GetBytes(pChars, value.Length, pBytes, byteCount);
            offset += written;
        }
    }

    /// <summary>
    /// Writes a string to the specified offset in the byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteString(
        this byte[] buffer,
        string value, ref int offset,
        System.Text.Encoding encoding = null)
    {
        if (value == null)
        {
            buffer.WriteInt32(-1, offset);
            offset += sizeof(int);
            return;
        }

        encoding ??= SerializationOptions.Encoding ??
            throw new System.ArgumentNullException(nameof(encoding));

        int byteCount = encoding.GetByteCount(value);

        if (buffer.Length < offset + sizeof(int) + byteCount)
            throw new System.ArgumentException("Buffer too small to write string.", nameof(buffer));

        buffer.WriteInt32(byteCount, offset); // Write string byte length
        offset += sizeof(int);

        fixed (char* pChars = value)
        fixed (byte* pBytes = &buffer[offset])
        {
            int written = encoding.GetBytes(pChars, value.Length, pBytes, byteCount);
            offset += written;
        }
    }
}
