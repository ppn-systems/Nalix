// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// High-performance helpers for encoding and decoding LEB128 VarInts.
/// Used for Minecraft-style packet framing.
/// </summary>
internal static class VarInt
{
    private const int MaxVarIntBytes = 5;

    /// <summary>
    /// Computes the number of bytes required to encode the given value as a VarInt.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetByteCount(int value)
    {
        uint uval = (uint)value;
        if (uval < 1 << 7)
        {
            return 1;
        }

        if (uval < 1 << 14)
        {
            return 2;
        }

        if (uval < 1 << 21)
        {
            return 3;
        }

        if (uval < 1 << 28)
        {
            return 4;
        }

        return 5;
    }

    /// <summary>
    /// Writes the integer value to the destination span as a VarInt.
    /// Returns the number of bytes written.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Write(Span<byte> destination, int value)
    {
        uint uval = (uint)value;
        int bytesWritten = 0;

        while (true)
        {
            if ((uval & ~0x7Fu) == 0)
            {
                destination[bytesWritten++] = (byte)uval;
                return bytesWritten;
            }

            destination[bytesWritten++] = (byte)((uval & 0x7F) | 0x80);
            uval >>= 7;
        }
    }

    /// <summary>
    /// Attempts to read a VarInt from the source span.
    /// Returns true if successful, setting <paramref name="value"/> to the decoded integer
    /// and <paramref name="bytesRead"/> to the number of bytes consumed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryRead(ReadOnlySpan<byte> source, out int value, out int bytesRead)
    {
        int result = 0;
        int shift = 0;
        bytesRead = 0;

        while (bytesRead < source.Length && bytesRead < MaxVarIntBytes)
        {
            byte b = source[bytesRead];
            result |= (b & 0x7F) << shift;
            bytesRead++;

            if ((b & 0x80) == 0)
            {
                value = result;
                return true;
            }

            shift += 7;
        }

        if (bytesRead >= MaxVarIntBytes)
        {
            throw new FormatException("VarInt is too big (overlong byte sequence).");
        }

        // Incomplete VarInt
        value = 0;
        bytesRead = 0;
        return false;
    }
}
