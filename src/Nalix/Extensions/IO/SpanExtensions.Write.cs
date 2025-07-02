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
    public static void WriteSByte(this System.Span<byte> span, in sbyte value, ref int offset)
    {
        if (span.Length < offset + 1)
            throw new System.ArgumentException("Span too small to write sbyte.", nameof(span));
        // Write the sbyte value directly to the span
        span[offset] = unchecked((byte)value);
        offset += sizeof(sbyte);
    }

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteSByte(this byte[] array, in sbyte value, ref int offset)
    {
        if (array.Length < offset + 1)
            throw new System.ArgumentException("Array too small to write sbyte.", nameof(array));
        // Write the sbyte value directly to the array
        array[offset] = unchecked((byte)value);
        offset += sizeof(sbyte);
    }

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteBool(this System.Span<byte> span, in bool value, ref int offset)
    {
        if (span.Length < offset + 1)
            throw new System.ArgumentException("Span too small to write bool.", nameof(span));
        // Write the boolean value as a byte (1 for true, 0 for false)
        span[offset] = value ? (byte)1 : (byte)0;
        offset += sizeof(bool);
    }

    /// <summary>
    /// Writes an 8-bit signed integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit signed integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteBool(this byte[] array, in bool value, ref int offset)
    {
        if (array.Length < offset + 1)
            throw new System.ArgumentException("Array too small to write bool.", nameof(array));
        // Write the boolean value as a byte (1 for true, 0 for false)
        array[offset] = value ? (byte)1 : (byte)0;
        offset += sizeof(bool);
    }

    /// <summary>
    /// Writes an 8-bit unsigned integer to the specified offset in the span.
    /// </summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The 8-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the span where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this System.Span<byte> span, in byte value, ref int offset)
    {
        if (span.Length < offset + 1)
            throw new System.ArgumentException("Span too small to write byte.", nameof(span));
        // Write the byte value directly to the span
        span[offset] = value;
        offset += sizeof(byte);
    }

    /// <summary>
    /// Writes an 8-bit unsigned integer to the specified offset in the byte array.
    /// </summary>
    /// <param name="array">The byte array to write to.</param>
    /// <param name="value">The 8-bit unsigned integer to write.</param>
    /// <param name="offset">The zero-based offset in the array where writing begins. Defaults to 0.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this byte[] array, in byte value, ref int offset)
    {
        array[offset] = value;
        offset += sizeof(byte);
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
    public static unsafe void WriteInt16(this System.Span<byte> span, in short value, ref int offset)
    {
        if (span.Length < offset + 2)
            throw new System.ArgumentException("Span too small to write Int16.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(short*)ptr = value;
        }

        offset += sizeof(short);
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
    public static unsafe void WriteInt16(this byte[] array, in short value, ref int offset)
    {
        if (array.Length < offset + 2)
            throw new System.ArgumentException("Array too small to write Int16.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(short*)ptr = value;
        }

        offset += sizeof(short);
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
    public static unsafe void WriteUInt16(this System.Span<byte> span, in ushort value, ref int offset)
    {
        if (span.Length < offset + 2)
            throw new System.ArgumentException("Span too small to write UInt16.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(ushort*)ptr = value;
        }

        offset += sizeof(ushort);
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
    public static unsafe void WriteUInt16(this byte[] array, in ushort value, ref int offset)
    {
        if (array.Length < offset + 2)
            throw new System.ArgumentException("Array too small to write UInt16.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(ushort*)ptr = value;
        }

        offset += sizeof(ushort);
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
    public static unsafe void WriteInt32(this System.Span<byte> span, in int value, ref int offset)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to write Int32.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(int*)ptr = value;
        }

        offset += sizeof(int);
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
    public static unsafe void WriteInt32(this byte[] array, in int value, ref int offset)
    {
        if (array.Length < offset + 4)
            throw new System.ArgumentException("Array too small to write Int32.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(int*)ptr = value;
        }

        offset += sizeof(int);
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
    public static unsafe void WriteUInt32(this System.Span<byte> span, in uint value, ref int offset)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to write UInt32.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(uint*)ptr = value;
        }

        offset += sizeof(uint);
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
    public static unsafe void WriteUInt32(this byte[] array, in uint value, ref int offset)
    {
        if (array.Length < offset + 4)
            throw new System.ArgumentException("Array too small to write UInt32.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(uint*)ptr = value;
        }

        offset += sizeof(uint);
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
    public static unsafe void WriteInt64(this System.Span<byte> span, in long value, ref int offset)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to write Int64.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(long*)ptr = value;
        }

        offset += sizeof(long);
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
    public static unsafe void WriteInt64(this byte[] array, in long value, ref int offset)
    {
        if (array.Length < offset + 8)
            throw new System.ArgumentException("Array too small to write Int64.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(long*)ptr = value;
        }

        offset += sizeof(long);
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
    public static unsafe void WriteUInt64(this System.Span<byte> span, in ulong value, ref int offset)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to write UInt64.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(ulong*)ptr = value;
        }

        offset += sizeof(ulong);
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
    public static unsafe void WriteUInt64(this byte[] array, in ulong value, ref int offset)
    {
        if (array.Length < offset + 8)
            throw new System.ArgumentException("Array too small to write UInt64.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(ulong*)ptr = value;
        }

        offset += sizeof(ulong);
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
    public static unsafe void WriteSingle(this System.Span<byte> span, in float value, ref int offset)
    {
        if (span.Length < offset + 4)
            throw new System.ArgumentException("Span too small to write Single.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(float*)ptr = value;
        }

        offset += sizeof(float);
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
    public static unsafe void WriteSingle(this byte[] array, in float value, ref int offset)
    {
        if (array.Length < offset + 4)
            throw new System.ArgumentException("Array too small to write Single.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(float*)ptr = value;
        }

        offset += sizeof(float);
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
    public static unsafe void WriteDouble(this System.Span<byte> span, in double value, ref int offset)
    {
        if (span.Length < offset + 8)
            throw new System.ArgumentException("Span too small to write Double.", nameof(span));

        fixed (byte* ptr = &span[offset])
        {
            *(double*)ptr = value;
        }

        offset += sizeof(double);
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
    public static unsafe void WriteDouble(this byte[] array, in double value, ref int offset)
    {
        if (array.Length < offset + 8)
            throw new System.ArgumentException("Array too small to write Double.", nameof(array));

        fixed (byte* ptr = &array[offset])
        {
            *(double*)ptr = value;
        }

        offset += sizeof(double);
    }

    /// <summary>
    /// Writes a character to the specified offset in the span.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteChar(this System.Span<byte> span, in char value, ref int offset)
    {
        if (offset < 0 || offset + 4 > span.Length)
            throw new System.ArgumentOutOfRangeException(nameof(offset));

        System.Span<byte> temp = stackalloc byte[4];

        int bytesWritten;
        fixed (char* pChar = &value)
        fixed (byte* pTemp = temp)
        {
            bytesWritten = System.Text.Encoding.UTF8.GetBytes(pChar, 1, pTemp, 4);
        }

        if (bytesWritten > 4)
            throw new System.InvalidOperationException("UTF-8 encoding of char exceeded 4 bytes.");

        for (int i = 0; i < 4; i++)
            span[offset + i] = i < bytesWritten ? temp[i] : (byte)0;

        offset += 4;
    }

    /// <summary>
    /// Writes a character to the specified offset in the span.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteChar(this byte[] buffer, in char value, ref int offset)
    {
        if (offset < 0 || offset + 4 > buffer.Length)
            throw new System.ArgumentOutOfRangeException(nameof(offset));

        System.Span<byte> temp = stackalloc byte[4];

        int bytesWritten;
        fixed (char* pChar = &value)
        fixed (byte* pTemp = temp)
        {
            bytesWritten = System.Text.Encoding.UTF8.GetBytes(pChar, 1, pTemp, 4);
        }

        if (bytesWritten > 4)
            throw new System.InvalidOperationException("UTF-8 encoding of char exceeded 4 bytes.");

        for (int i = 0; i < 4; i++)
            buffer[offset + i] = i < bytesWritten ? temp[i] : (byte)0;

        offset += 4;
    }

    /// <summary>
    /// Writes a character to the specified offset in the byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteString(this System.Span<byte> span, string value, ref int offset)
    {
        if (value == null)
        {
            span.WriteInt32(-1, ref offset);
            return;
        }

        int byteCount = System.Text.Encoding.UTF8.GetByteCount(value);

        if (span.Length < offset + sizeof(int) + byteCount)
            throw new System.ArgumentException("Span too small to write string.", nameof(span));

        // Write string length
        span.WriteInt32(byteCount, ref offset);

        fixed (char* pChars = value)
        fixed (byte* pBytes = &span[offset])
        {
            int written = System.Text.Encoding.UTF8.GetBytes(pChars, value.Length, pBytes, byteCount);
            offset += written;
        }
    }

    /// <summary>
    /// Writes a string to the specified offset in the byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteString(this byte[] buffer, string value, ref int offset)
    {
        if (value == null)
        {
            buffer.WriteInt32(-1, ref offset);
            return;
        }

        int byteCount = System.Text.Encoding.UTF8.GetByteCount(value);

        if (buffer.Length < offset + sizeof(int) + byteCount)
            throw new System.ArgumentException("Buffer too small to write string.", nameof(buffer));

        // Write string byte length
        buffer.WriteInt32(byteCount, ref offset);

        fixed (char* pChars = value)
        fixed (byte* pBytes = &buffer[offset])
        {
            int written = System.Text.Encoding.UTF8.GetBytes(pChars, value.Length, pBytes, byteCount);
            offset += written;
        }
    }
}