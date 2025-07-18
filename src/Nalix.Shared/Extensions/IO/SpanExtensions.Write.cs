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
    public static void WriteSByte(this System.Span<System.Byte> span, in System.SByte value, ref System.Int32 offset)
    {
        if (span.Length < offset + 1)
        {
            throw new System.ArgumentException("Span too small to write sbyte.", nameof(span));
        }
        // Write the sbyte value directly to the span
        span[offset] = unchecked((System.Byte)value);
        offset += sizeof(System.SByte);
    }

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteSByte(this System.Byte[] array, in System.SByte value, ref System.Int32 offset)
    {
        if (array.Length < offset + 1)
        {
            throw new System.ArgumentException("Array too small to write sbyte.", nameof(array));
        }
        // Write the sbyte value directly to the array
        array[offset] = unchecked((System.Byte)value);
        offset += sizeof(System.SByte);
    }

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteBool(this System.Span<System.Byte> span, in System.Boolean value, ref System.Int32 offset)
    {
        if (span.Length < offset + 1)
        {
            throw new System.ArgumentException("Span too small to write bool.", nameof(span));
        }
        // Write the boolean value as a byte (1 for true, 0 for false)
        span[offset] = value ? (System.Byte)1 : (System.Byte)0;
        offset += sizeof(System.Boolean);
    }

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteBool(this System.Byte[] array, in System.Boolean value, ref System.Int32 offset)
    {
        if (array.Length < offset + 1)
        {
            throw new System.ArgumentException("Array too small to write bool.", nameof(array));
        }
        // Write the boolean value as a byte (1 for true, 0 for false)
        array[offset] = value ? (System.Byte)1 : (System.Byte)0;
        offset += sizeof(System.Boolean);
    }

    /// <summary>
    /// Writes an 8-bit unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 8-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this System.Span<System.Byte> span, in System.Byte value, ref System.Int32 offset)
    {
        if (span.Length < offset + 1)
        {
            throw new System.ArgumentException("Span too small to write byte.", nameof(span));
        }
        // Write the byte value directly to the span
        span[offset] = value;
        offset += sizeof(System.Byte);
    }

    /// <summary>
    /// Writes an 8-bit unsigned integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this System.Byte[] array, in System.Byte value, ref System.Int32 offset)
    {
        array[offset] = value;
        offset += sizeof(System.Byte);
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
    public static unsafe void WriteInt16(this System.Span<System.Byte> span, in System.Int16 value, ref System.Int32 offset)
    {
        if (span.Length < offset + 2)
        {
            throw new System.ArgumentException("Span too small to write Int16.", nameof(span));
        }

        fixed (System.Byte* ptr = &span[offset])
        {
            *(System.Int16*)ptr = value;
        }

        offset += sizeof(System.Int16);
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
    public static unsafe void WriteInt16(this System.Byte[] array, in System.Int16 value, ref System.Int32 offset)
    {
        if (array.Length < offset + 2)
        {
            throw new System.ArgumentException("Array too small to write Int16.", nameof(array));
        }

        fixed (System.Byte* ptr = &array[offset])
        {
            *(System.Int16*)ptr = value;
        }

        offset += sizeof(System.Int16);
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
    public static unsafe void WriteUInt16(this System.Span<System.Byte> span, in System.UInt16 value, ref System.Int32 offset)
    {
        if (span.Length < offset + 2)
        {
            throw new System.ArgumentException("Span too small to write UInt16.", nameof(span));
        }

        fixed (System.Byte* ptr = &span[offset])
        {
            *(System.UInt16*)ptr = value;
        }

        offset += sizeof(System.UInt16);
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
    public static unsafe void WriteUInt16(this System.Byte[] array, in System.UInt16 value, ref System.Int32 offset)
    {
        if (array.Length < offset + 2)
        {
            throw new System.ArgumentException("Array too small to write UInt16.", nameof(array));
        }

        fixed (System.Byte* ptr = &array[offset])
        {
            *(System.UInt16*)ptr = value;
        }

        offset += sizeof(System.UInt16);
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
    public static unsafe void WriteInt32(this System.Span<System.Byte> span, in System.Int32 value, ref System.Int32 offset)
    {
        if (span.Length < offset + 4)
        {
            throw new System.ArgumentException("Span too small to write Int32.", nameof(span));
        }

        fixed (System.Byte* ptr = &span[offset])
        {
            *(System.Int32*)ptr = value;
        }

        offset += sizeof(System.Int32);
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
    public static unsafe void WriteInt32(this System.Byte[] array, in System.Int32 value, ref System.Int32 offset)
    {
        if (array.Length < offset + 4)
        {
            throw new System.ArgumentException("Array too small to write Int32.", nameof(array));
        }

        fixed (System.Byte* ptr = &array[offset])
        {
            *(System.Int32*)ptr = value;
        }

        offset += sizeof(System.Int32);
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
    public static unsafe void WriteUInt32(this System.Span<System.Byte> span, in System.UInt32 value, ref System.Int32 offset)
    {
        if (span.Length < offset + 4)
        {
            throw new System.ArgumentException("Span too small to write UInt32.", nameof(span));
        }

        fixed (System.Byte* ptr = &span[offset])
        {
            *(System.UInt32*)ptr = value;
        }

        offset += sizeof(System.UInt32);
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
    public static unsafe void WriteUInt32(this System.Byte[] array, in System.UInt32 value, ref System.Int32 offset)
    {
        if (array.Length < offset + 4)
        {
            throw new System.ArgumentException("Array too small to write UInt32.", nameof(array));
        }

        fixed (System.Byte* ptr = &array[offset])
        {
            *(System.UInt32*)ptr = value;
        }

        offset += sizeof(System.UInt32);
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
    public static unsafe void WriteInt64(this System.Span<System.Byte> span, in System.Int64 value, ref System.Int32 offset)
    {
        if (span.Length < offset + 8)
        {
            throw new System.ArgumentException("Span too small to write Int64.", nameof(span));
        }

        fixed (System.Byte* ptr = &span[offset])
        {
            *(System.Int64*)ptr = value;
        }

        offset += sizeof(System.Int64);
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
    public static unsafe void WriteInt64(this System.Byte[] array, in System.Int64 value, ref System.Int32 offset)
    {
        if (array.Length < offset + 8)
        {
            throw new System.ArgumentException("Array too small to write Int64.", nameof(array));
        }

        fixed (System.Byte* ptr = &array[offset])
        {
            *(System.Int64*)ptr = value;
        }

        offset += sizeof(System.Int64);
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
    public static unsafe void WriteUInt64(this System.Span<System.Byte> span, in System.UInt64 value, ref System.Int32 offset)
    {
        if (span.Length < offset + 8)
        {
            throw new System.ArgumentException("Span too small to write UInt64.", nameof(span));
        }

        fixed (System.Byte* ptr = &span[offset])
        {
            *(System.UInt64*)ptr = value;
        }

        offset += sizeof(System.UInt64);
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
    public static unsafe void WriteUInt64(this System.Byte[] array, in System.UInt64 value, ref System.Int32 offset)
    {
        if (array.Length < offset + 8)
        {
            throw new System.ArgumentException("Array too small to write UInt64.", nameof(array));
        }

        fixed (System.Byte* ptr = &array[offset])
        {
            *(System.UInt64*)ptr = value;
        }

        offset += sizeof(System.UInt64);
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
    public static unsafe void WriteSingle(this System.Span<System.Byte> span, in System.Single value, ref System.Int32 offset)
    {
        if (span.Length < offset + 4)
        {
            throw new System.ArgumentException("Span too small to write Single.", nameof(span));
        }

        fixed (System.Byte* ptr = &span[offset])
        {
            *(System.Single*)ptr = value;
        }

        offset += sizeof(System.Single);
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
    public static unsafe void WriteSingle(this System.Byte[] array, in System.Single value, ref System.Int32 offset)
    {
        if (array.Length < offset + 4)
        {
            throw new System.ArgumentException("Array too small to write Single.", nameof(array));
        }

        fixed (System.Byte* ptr = &array[offset])
        {
            *(System.Single*)ptr = value;
        }

        offset += sizeof(System.Single);
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
    public static unsafe void WriteDouble(this System.Span<System.Byte> span, in System.Double value, ref System.Int32 offset)
    {
        if (span.Length < offset + 8)
        {
            throw new System.ArgumentException("Span too small to write Double.", nameof(span));
        }

        fixed (System.Byte* ptr = &span[offset])
        {
            *(System.Double*)ptr = value;
        }

        offset += sizeof(System.Double);
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
    public static unsafe void WriteDouble(this System.Byte[] array, in System.Double value, ref System.Int32 offset)
    {
        if (array.Length < offset + 8)
        {
            throw new System.ArgumentException("Array too small to write Double.", nameof(array));
        }

        fixed (System.Byte* ptr = &array[offset])
        {
            *(System.Double*)ptr = value;
        }

        offset += sizeof(System.Double);
    }

    /// <summary>
    /// Writes a character to the specified offset in the span.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteChar(this System.Span<System.Byte> span, in System.Char value, ref System.Int32 offset)
    {
        if (offset < 0 || offset + 4 > span.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(offset));
        }

        System.Span<System.Byte> temp = stackalloc System.Byte[4];

        System.Int32 bytesWritten;
        fixed (System.Char* pChar = &value)
        fixed (System.Byte* pTemp = temp)
        {
            bytesWritten = System.Text.Encoding.UTF8.GetBytes(pChar, 1, pTemp, 4);
        }

        if (bytesWritten > 4)
        {
            throw new System.InvalidOperationException("UTF-8 encoding of char exceeded 4 bytes.");
        }

        for (System.Int32 i = 0; i < 4; i++)
        {
            span[offset + i] = i < bytesWritten ? temp[i] : (System.Byte)0;
        }

        offset += 4;
    }

    /// <summary>
    /// Writes a character to the specified offset in the span.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteChar(this System.Byte[] buffer, in System.Char value, ref System.Int32 offset)
    {
        if (offset < 0 || offset + 4 > buffer.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(offset));
        }

        System.Span<System.Byte> temp = stackalloc System.Byte[4];

        System.Int32 bytesWritten;
        fixed (System.Char* pChar = &value)
        fixed (System.Byte* pTemp = temp)
        {
            bytesWritten = System.Text.Encoding.UTF8.GetBytes(pChar, 1, pTemp, 4);
        }

        if (bytesWritten > 4)
        {
            throw new System.InvalidOperationException("UTF-8 encoding of char exceeded 4 bytes.");
        }

        for (System.Int32 i = 0; i < 4; i++)
        {
            buffer[offset + i] = i < bytesWritten ? temp[i] : (System.Byte)0;
        }

        offset += 4;
    }

    /// <summary>
    /// Writes a character to the specified offset in the byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteString(this System.Span<System.Byte> span, System.String value, ref System.Int32 offset)
    {
        if (value == null)
        {
            span.WriteInt32(-1, ref offset);
            return;
        }

        System.Int32 byteCount = System.Text.Encoding.UTF8.GetByteCount(value);

        if (span.Length < offset + sizeof(System.Int32) + byteCount)
        {
            throw new System.ArgumentException("Span too small to write string.", nameof(span));
        }

        // Write string length
        span.WriteInt32(byteCount, ref offset);

        fixed (System.Char* pChars = value)
        fixed (System.Byte* pBytes = &span[offset])
        {
            System.Int32 written = System.Text.Encoding.UTF8.GetBytes(pChars, value.Length, pBytes, byteCount);
            offset += written;
        }
    }

    /// <summary>
    /// Writes a string to the specified offset in the byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteString(this System.Byte[] buffer, System.String value, ref System.Int32 offset)
    {
        if (value == null)
        {
            buffer.WriteInt32(-1, ref offset);
            return;
        }

        System.Int32 byteCount = System.Text.Encoding.UTF8.GetByteCount(value);

        if (buffer.Length < offset + sizeof(System.Int32) + byteCount)
        {
            throw new System.ArgumentException("Buffer too small to write string.", nameof(buffer));
        }

        // Write string byte length
        buffer.WriteInt32(byteCount, ref offset);

        fixed (System.Char* pChars = value)
        fixed (System.Byte* pBytes = &buffer[offset])
        {
            System.Int32 written = System.Text.Encoding.UTF8.GetBytes(pChars, value.Length, pBytes, byteCount);
            offset += written;
        }
    }
}