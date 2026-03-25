// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Extension methods for writing primitive and common types to <see cref="DataWriter"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DataWriterExtensions
{
    #region Primitive Types

    /// <summary>
    /// Writes a <see cref="byte"/> to the buffer.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, byte value)
    {
        writer.Expand(sizeof(byte));
        ref byte ptr = ref writer.GetFreeBufferReference();
        ptr = value;
        writer.Advance(sizeof(byte));
    }

    /// <summary>
    /// Writes a <see cref="ushort"/> to the buffer.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, ushort value)
    {
        writer.Expand(sizeof(ushort));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(ushort));
    }

    /// <summary>
    /// Writes a <see cref="uint"/> to the buffer.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, uint value)
    {
        writer.Expand(sizeof(uint));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(uint));
    }

    /// <summary>
    /// Writes a <see cref="int"/> to the buffer.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, int value)
    {
        writer.Expand(sizeof(int));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(int));
    }

    /// <summary>
    /// Writes a <see cref="long"/> to the buffer.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, long value)
    {
        writer.Expand(sizeof(long));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(long));
    }

    /// <summary>
    /// Writes a <see cref="ulong"/> to the buffer.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, ulong value)
    {
        writer.Expand(sizeof(ulong));
        ref byte ptr = ref writer.GetFreeBufferReference();
        Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(ulong));
    }

    /// <summary>
    /// Writes a <see cref="bool"/> to the buffer.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, bool value) => writer.Write((byte)(value ? 1 : 0));

    #endregion Primitive Types

    #region Enum Types

    /// <summary>
    /// Writes an enum with <see cref="byte"/> underlying type.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <exception cref="NotSupportedException"></exception>
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
