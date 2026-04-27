// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Nalix.Codec.Memory;

namespace Nalix.Codec.Extensions;

/// <summary>
/// Extension methods for writing primitive and common types to <see cref="DataWriter"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DataWriterExtensions
{
    #region Primitive Types

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, sbyte value)
    {
        writer.Expand(sizeof(sbyte));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(sbyte));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, byte value)
    {
        writer.Expand(sizeof(byte));
        ref byte ptr = ref writer.GetFreeBufferReference();
        ptr = value;
        writer.Advance(sizeof(byte));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, short value)
    {
        writer.Expand(sizeof(short));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(short));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, ushort value)
    {
        writer.Expand(sizeof(ushort));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(ushort));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, uint value)
    {
        writer.Expand(sizeof(uint));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(uint));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, int value)
    {
        writer.Expand(sizeof(int));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(int));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, long value)
    {
        writer.Expand(sizeof(long));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(long));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, ulong value)
    {
        writer.Expand(sizeof(ulong));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(ulong));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, bool value) => writer.Write((byte)(value ? 1 : 0));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, char value)
    {
        writer.Expand(sizeof(char));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(char));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, float value)
    {
        writer.Expand(sizeof(float));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(float));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, double value)
    {
        writer.Expand(sizeof(double));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(double));
    }

    #endregion Primitive Types

    #region Enum Types

    /// <summary>
    /// Writes an enum with <see cref="byte"/> underlying type.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <exception cref="NotSupportedException">Thrown when <typeparamref name="TEnum"/> uses an unsupported underlying type.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteEnum<TEnum>(this ref DataWriter writer, TEnum value) where TEnum : Enum
    {
        Type underlyingType = Enum.GetUnderlyingType(typeof(TEnum));

        if (underlyingType == typeof(byte))
        {
            writer.Write(Unsafe.As<TEnum, byte>(ref value));
        }
        else if (underlyingType == typeof(ushort))
        {
            writer.Write(Unsafe.As<TEnum, ushort>(ref value));
        }
        else if (underlyingType == typeof(uint))
        {
            writer.Write(Unsafe.As<TEnum, uint>(ref value));
        }
        else if (underlyingType == typeof(int))
        {
            writer.Write(Unsafe.As<TEnum, int>(ref value));
        }
        else
        {
            throw new NotSupportedException($"Enum underlying type {underlyingType.Name} not supported.");
        }
    }

    #endregion Enum Types

    #region Array Types

    /// <summary>
    /// Writes a byte array to the buffer.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <exception cref="InvalidOperationException">Thrown when the writer wraps a fixed buffer that cannot expand to fit <paramref name="value"/>.</exception>
    /// <exception cref="OutOfMemoryException">Thrown when the writer cannot rent a larger backing buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, byte[] value)
    {
        if (value is null || value.Length == 0)
        {
            return;
        }

        writer.Expand(value.Length);
        ref byte dst = ref writer.GetFreeBufferReference();

        unsafe
        {
            fixed (byte* pSrc = value)
            {
                fixed (byte* pDst = &dst)
                {
                    Unsafe.CopyBlockUnaligned(pDst, pSrc, (uint)value.Length);
                }
            }
        }

        writer.Advance(value.Length);
    }

    /// <summary>
    /// Writes a span of bytes to the buffer.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <exception cref="InvalidOperationException">Thrown when the writer wraps a fixed buffer that cannot expand to fit <paramref name="value"/>.</exception>
    /// <exception cref="OutOfMemoryException">Thrown when the writer cannot rent a larger backing buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        writer.Expand(value.Length);
        value.CopyTo(writer.FreeBuffer);
        writer.Advance(value.Length);
    }

    #endregion Array Types

    #region Generic Unmanaged

    /// <summary>
    /// Writes any unmanaged type directly to buffer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <exception cref="InvalidOperationException">Thrown when the writer wraps a fixed buffer that cannot expand to fit the unmanaged value.</exception>
    /// <exception cref="OutOfMemoryException">Thrown when the writer cannot rent a larger backing buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUnmanaged<T>(this ref DataWriter writer, T value) where T : unmanaged
    {
        writer.Expand(Unsafe.SizeOf<T>());
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(Unsafe.SizeOf<T>());
    }

    #endregion Generic Unmanaged
}
