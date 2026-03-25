// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Extension methods for writing primitive and common types to <see cref="DataWriter"/>.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class DataWriterExtensions
{
    #region Primitive Types

    /// <summary>
    /// Writes a <see cref="Byte"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, Byte value)
    {
        writer.Expand(sizeof(Byte));
        ref Byte ptr = ref writer.GetFreeBufferReference();
        ptr = value;
        writer.Advance(sizeof(Byte));
    }

    /// <summary>
    /// Writes a <see cref="UInt16"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, UInt16 value)
    {
        writer.Expand(sizeof(UInt16));
        ref Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(UInt16));
    }

    /// <summary>
    /// Writes a <see cref="UInt32"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, UInt32 value)
    {
        writer.Expand(sizeof(UInt32));
        ref Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(UInt32));
    }

    /// <summary>
    /// Writes a <see cref="Int32"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, Int32 value)
    {
        writer.Expand(sizeof(Int32));
        ref Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(Int32));
    }

    /// <summary>
    /// Writes a <see cref="Int64"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, Int64 value)
    {
        writer.Expand(sizeof(Int64));
        ref Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(Int64));
    }

    /// <summary>
    /// Writes a <see cref="UInt64"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, UInt64 value)
    {
        writer.Expand(sizeof(UInt64));
        ref Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(UInt64));
    }

    /// <summary>
    /// Writes a <see cref="Boolean"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, Boolean value) => writer.Write((Byte)(value ? 1 : 0));

    #endregion

    #region Enum Types

    /// <summary>
    /// Writes an enum with <see cref="Byte"/> underlying type.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteEnum<TEnum>(this ref DataWriter writer, TEnum value) where TEnum : Enum
    {
        Type underlyingType = Enum.GetUnderlyingType(typeof(TEnum));

        if (underlyingType == typeof(Byte))
        {
            writer.Write(System.Runtime.CompilerServices.Unsafe.As<TEnum, Byte>(ref value));
        }
        else if (underlyingType == typeof(UInt16))
        {
            writer.Write(System.Runtime.CompilerServices.Unsafe.As<TEnum, UInt16>(ref value));
        }
        else if (underlyingType == typeof(UInt32))
        {
            writer.Write(System.Runtime.CompilerServices.Unsafe.As<TEnum, UInt32>(ref value));
        }
        else if (underlyingType == typeof(Int32))
        {
            writer.Write(System.Runtime.CompilerServices.Unsafe.As<TEnum, Int32>(ref value));
        }
        else
        {
            throw new NotSupportedException($"Enum underlying type {underlyingType.Name} not supported.");
        }
    }

    #endregion

    #region Array Types

    /// <summary>
    /// Writes a byte array to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, Byte[] value)
    {
        if (value is null || value.Length == 0)
        {
            return;
        }

        writer.Expand(value.Length);
        ref Byte dst = ref writer.GetFreeBufferReference();

        unsafe
        {
            fixed (Byte* pSrc = value)
            {
                fixed (Byte* pDst = &dst)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(pDst, pSrc, (UInt32)value.Length);
                }
            }
        }

        writer.Advance(value.Length);
    }

    /// <summary>
    /// Writes a span of bytes to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, ReadOnlySpan<Byte> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        writer.Expand(value.Length);
        value.CopyTo(writer.FreeBuffer);
        writer.Advance(value.Length);
    }

    #endregion

    #region Generic Unmanaged

    /// <summary>
    /// Writes any unmanaged type directly to buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteUnmanaged<T>(this ref DataWriter writer, T value) where T : unmanaged
    {
        writer.Expand(System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        ref Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
    }

    #endregion
}
