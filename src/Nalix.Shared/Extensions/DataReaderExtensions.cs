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
    /// Reads a <see cref="byte"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(this ref DataReader reader)
    {
        ref byte ptr = ref reader.GetSpanReference(sizeof(byte));
        byte value = ptr;
        reader.Advance(sizeof(byte));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="ushort"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16(this ref DataReader reader)
    {
        ref byte ptr = ref reader.GetSpanReference(sizeof(ushort));
        ushort value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<ushort>(ref ptr);
        reader.Advance(sizeof(ushort));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="uint"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32(this ref DataReader reader)
    {
        ref byte ptr = ref reader.GetSpanReference(sizeof(uint));
        uint value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<uint>(ref ptr);
        reader.Advance(sizeof(uint));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="int"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(this ref DataReader reader)
    {
        ref byte ptr = ref reader.GetSpanReference(sizeof(int));
        int value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<int>(ref ptr);
        reader.Advance(sizeof(int));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="long"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static long ReadInt64(this ref DataReader reader)
    {
        ref byte ptr = ref reader.GetSpanReference(sizeof(long));
        long value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<long>(ref ptr);
        reader.Advance(sizeof(long));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="ulong"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ulong ReadUInt64(this ref DataReader reader)
    {
        ref byte ptr = ref reader.GetSpanReference(sizeof(ulong));
        ulong value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<ulong>(ref ptr);
        reader.Advance(sizeof(ulong));
        return value;
    }

    /// <summary>
    /// Reads a <see cref="bool"/> from the buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool ReadBoolean(this ref DataReader reader) => reader.ReadByte() != 0;

    #endregion Primitive Types

    #region Enum Types

    /// <summary>
    /// Reads an enum value with underlying type <see cref="byte"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum ReadEnumByte<TEnum>(this ref DataReader reader)
    where TEnum : unmanaged, System.Enum
    {
        byte value = reader.ReadByte();
        return System.Runtime.CompilerServices.Unsafe.As<byte, TEnum>(ref value);
    }

    /// <summary>
    /// Reads an enum value with underlying type <see cref="ushort"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum ReadEnumUInt16<TEnum>(this ref DataReader reader)
    where TEnum : unmanaged, System.Enum
    {
        ushort value = reader.ReadUInt16();
        return System.Runtime.CompilerServices.Unsafe.As<ushort, TEnum>(ref value);
    }

    /// <summary>
    /// Reads an enum value with underlying type <see cref="uint"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum ReadEnumUInt32<TEnum>(this ref DataReader reader)
    where TEnum : unmanaged, System.Enum
    {
        uint value = reader.ReadUInt32();
        return System.Runtime.CompilerServices.Unsafe.As<uint, TEnum>(ref value);
    }

    #endregion Enum Types

    #region Array Types

    /// <summary>
    /// Reads a byte array with specified length.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte[] ReadBytes(this ref DataReader reader, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        ref byte ptr = ref reader.GetSpanReference(count);
        byte[] result = new byte[count];

        unsafe
        {
            fixed (byte* pSrc = &ptr)
            {
                fixed (byte* pDst = result)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(pDst, pSrc, (uint)count);
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
    public static byte[] ReadRemainingBytes(this ref DataReader reader) => reader.ReadBytes(reader.BytesRemaining);

    #endregion Array Types

    #region Generic Unmanaged

    /// <summary>
    /// Reads any unmanaged type directly from buffer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static T ReadUnmanaged<T>(this ref DataReader reader) where T : unmanaged
    {
        ref byte ptr = ref reader.GetSpanReference(System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        T value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(ref ptr);
        reader.Advance(System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        return value;
    }

    #endregion Generic Unmanaged

    #region Helper Properties

    /// <summary>
    /// Gets remaining byte count without consuming.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int Remaining(this ref DataReader reader) => reader.BytesRemaining;

    #endregion Helper Properties
}
