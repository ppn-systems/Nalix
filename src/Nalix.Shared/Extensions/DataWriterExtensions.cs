// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
    /// Writes a <see cref="System.Byte"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, System.Byte value)
    {
        writer.Expand(sizeof(System.Byte));
        ref System.Byte ptr = ref writer.GetFreeBufferReference();
        ptr = value;
        writer.Advance(sizeof(System.Byte));
    }

    /// <summary>
    /// Writes a <see cref="System.UInt16"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, System.UInt16 value)
    {
        writer.Expand(sizeof(System.UInt16));
        ref System.Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(System.UInt16));
    }

    /// <summary>
    /// Writes a <see cref="System.UInt32"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, System.UInt32 value)
    {
        writer.Expand(sizeof(System.UInt32));
        ref System.Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(System.UInt32));
    }

    /// <summary>
    /// Writes a <see cref="System.Int32"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, System.Int32 value)
    {
        writer.Expand(sizeof(System.Int32));
        ref System.Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(System.Int32));
    }

    /// <summary>
    /// Writes a <see cref="System.Int64"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, System.Int64 value)
    {
        writer.Expand(sizeof(System.Int64));
        ref System.Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(System.Int64));
    }

    /// <summary>
    /// Writes a <see cref="System.UInt64"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, System.UInt64 value)
    {
        writer.Expand(sizeof(System.UInt64));
        ref System.Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(sizeof(System.UInt64));
    }

    /// <summary>
    /// Writes a <see cref="System.Boolean"/> to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, System.Boolean value) => writer.Write((System.Byte)(value ? 1 : 0));

    #endregion

    #region Enum Types

    /// <summary>
    /// Writes an enum with <see cref="System.Byte"/> underlying type.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteEnum<TEnum>(this ref DataWriter writer, TEnum value) where TEnum : System.Enum
    {
        var underlyingType = System.Enum.GetUnderlyingType(typeof(TEnum));

        if (underlyingType == typeof(System.Byte))
        {
            writer.Write(System.Runtime.CompilerServices.Unsafe.As<TEnum, System.Byte>(ref value));
        }
        else if (underlyingType == typeof(System.UInt16))
        {
            writer.Write(System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt16>(ref value));
        }
        else if (underlyingType == typeof(System.UInt32))
        {
            writer.Write(System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt32>(ref value));
        }
        else if (underlyingType == typeof(System.Int32))
        {
            writer.Write(System.Runtime.CompilerServices.Unsafe.As<TEnum, System.Int32>(ref value));
        }
        else
        {
            throw new System.NotSupportedException($"Enum underlying type {underlyingType.Name} not supported.");
        }
    }

    #endregion

    #region Array Types

    /// <summary>
    /// Writes a byte array to the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(this ref DataWriter writer, System.Byte[] value)
    {
        if (value is null || value.Length == 0)
        {
            return;
        }

        writer.Expand(value.Length);
        ref System.Byte dst = ref writer.GetFreeBufferReference();

        unsafe
        {
            fixed (System.Byte* pSrc = value)
            {
                fixed (System.Byte* pDst = &dst)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(pDst, pSrc, (System.UInt32)value.Length);
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
    public static void Write(this ref DataWriter writer, System.ReadOnlySpan<System.Byte> value)
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
        ref System.Byte ptr = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref ptr, value);
        writer.Advance(System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
    }

    #endregion
}
