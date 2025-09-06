// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Extension methods for reading primitive and common types from <see cref="DataReader"/>.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class DataReaderExtensions
{
    #region Primitive Types

    /// <summary>
    /// Reads a <see cref="System.Byte"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte ReadByte(this ref DataReader reader)
    {
        ref System.Byte ptr = ref reader.GetSpanReference(sizeof(System.Byte));
        System.Byte value = ptr;
        reader.Advance(sizeof(System.Byte));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="System.UInt16"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 ReadUInt16(this ref DataReader reader)
    {
        ref System.Byte ptr = ref reader.GetSpanReference(sizeof(System.UInt16));
        System.UInt16 value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt16>(ref ptr);
        reader.Advance(sizeof(System.UInt16));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="System.UInt32"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 ReadUInt32(this ref DataReader reader)
    {
        ref System.Byte ptr = ref reader.GetSpanReference(sizeof(System.UInt32));
        System.UInt32 value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(ref ptr);
        reader.Advance(sizeof(System.UInt32));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="System.Int32"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 ReadInt32(this ref DataReader reader)
    {
        ref System.Byte ptr = ref reader.GetSpanReference(sizeof(System.Int32));
        System.Int32 value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Int32>(ref ptr);
        reader.Advance(sizeof(System.Int32));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="System.Int64"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int64 ReadInt64(this ref DataReader reader)
    {
        ref System.Byte ptr = ref reader.GetSpanReference(sizeof(System.Int64));
        System.Int64 value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Int64>(ref ptr);
        reader.Advance(sizeof(System.Int64));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="System.UInt64"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt64 ReadUInt64(this ref DataReader reader)
    {
        ref System.Byte ptr = ref reader.GetSpanReference(sizeof(System.UInt64));
        System.UInt64 value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(ref ptr);
        reader.Advance(sizeof(System.UInt64));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="System.Boolean"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean ReadBoolean(this ref DataReader reader) => reader.ReadByte() != 0;

    #endregion

    #region Enum Types

    /// <summary>
    /// Reads an enum value with underlying type <see cref="System.Byte"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum ReadEnumByte<TEnum>(this ref DataReader reader)
    where TEnum : unmanaged, System.Enum
    {
        System.Byte value = reader.ReadByte();
        return System.Runtime.CompilerServices.Unsafe.As<System.Byte, TEnum>(ref value);
    }

    /// <summary>
    /// Reads an enum value with underlying type <see cref="System.UInt16"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum ReadEnumUInt16<TEnum>(this ref DataReader reader)
    where TEnum : unmanaged, System.Enum
    {
        System.UInt16 value = reader.ReadUInt16();
        return System.Runtime.CompilerServices.Unsafe.As<System.UInt16, TEnum>(ref value);
    }

    /// <summary>
    /// Reads an enum value with underlying type <see cref="System.UInt32"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum ReadEnumUInt32<TEnum>(this ref DataReader reader)
    where TEnum : unmanaged, System.Enum
    {
        System.UInt32 value = reader.ReadUInt32();
        return System.Runtime.CompilerServices.Unsafe.As<System.UInt32, TEnum>(ref value);
    }

    #endregion

    #region Array Types

    /// <summary>
    /// Reads a byte array with specified length.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] ReadBytes(this ref DataReader reader, System.Int32 count)
    {
        if (count <= 0)
        {
            return System.Array.Empty<System.Byte>();
        }

        ref System.Byte ptr = ref reader.GetSpanReference(count);
        System.Byte[] result = new System.Byte[count];

        unsafe
        {
            fixed (System.Byte* pSrc = &ptr)
            {
                fixed (System.Byte* pDst = result)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(pDst, pSrc, (System.UInt32)count);
                }
            }
        }

        reader.Advance(count);
        return result;
    }

    /// <summary>
    /// Reads remaining bytes as byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte[] ReadRemainingBytes(this ref DataReader reader) => reader.ReadBytes(reader.BytesRemaining);

    #endregion

    #region Generic Unmanaged

    /// <summary>
    /// Reads any unmanaged type directly from buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static T ReadUnmanaged<T>(this ref DataReader reader) where T : unmanaged
    {
        ref System.Byte ptr = ref reader.GetSpanReference(System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        T value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(ref ptr);
        reader.Advance(System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        return value;
    }

    #endregion

    #region Helper Properties

    /// <summary>
    /// Gets remaining byte count without consuming.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 Remaining(this ref DataReader reader) => reader.BytesRemaining;

    #endregion
}
