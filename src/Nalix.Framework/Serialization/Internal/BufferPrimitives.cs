// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.Serialization.Internal;

internal static class BufferPrimitives
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(ref DataWriter writer, byte value)
    {
        writer.Expand(sizeof(byte));
        writer.FreeBuffer[0] = value;
        writer.Advance(sizeof(byte));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(ref DataReader reader)
    {
        ref byte start = ref reader.GetSpanReference(sizeof(byte));
        byte value = start;
        reader.Advance(sizeof(byte));
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16(ref DataWriter writer, ushort value)
    {
        writer.Expand(sizeof(ushort));
        BinaryPrimitives.WriteUInt16LittleEndian(
            writer.FreeBuffer[..sizeof(ushort)], value);
        writer.Advance(sizeof(ushort));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16(ref DataReader reader)
    {
        ref byte start = ref reader.GetSpanReference(sizeof(ushort));
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(
            MemoryMarshal.CreateReadOnlySpan(ref start, sizeof(ushort)));
        reader.Advance(sizeof(ushort));
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt32(ref DataWriter writer, int value)
    {
        writer.Expand(sizeof(int));
        BinaryPrimitives.WriteInt32LittleEndian(
            writer.FreeBuffer[..sizeof(int)], value);
        writer.Advance(sizeof(int));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(ref DataReader reader)
    {
        ref byte start = ref reader.GetSpanReference(sizeof(int));
        int value = BinaryPrimitives.ReadInt32LittleEndian(
            MemoryMarshal.CreateReadOnlySpan(ref start, sizeof(int)));
        reader.Advance(sizeof(int));
        return value;
    }
}
