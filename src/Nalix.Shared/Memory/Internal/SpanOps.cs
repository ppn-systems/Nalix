// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Internal;

/// <summary>
/// Helper methods for working with Spans.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static unsafe class SpanOps
{
    /// <summary>
    /// Writes a variable-length integer (little-endian). Used for lengths greater than 15.
    /// Writes bytes until the value is less than 255.
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static int WriteVarInt(byte* dest, int value)
    {
        // Negative should never happen in encoder paths; clamp-to-0 preserves protocol.
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "WriteVarInt does not support negative values.");
        }

        // Number of full 0xFF bytes and the final remainder (<255)
        // This removes the loop-carried subtraction by 255.
        uint u = (uint)value;
        int ffCount = (int)(u / 255u);     // how many 0xFF
        byte last = (byte)(u % 255u);    // final terminator

        byte* p = dest;

        // Write 0xFF blocks with wide stores to reduce loop overhead
        const ulong F8 = 0xFFFFFFFFFFFFFFFFul;

        // 16-byte chunks (2x ulong)
        while (ffCount >= 16)
        {
            // write 16 bytes of 0xFF
            *(ulong*)p = F8; *(ulong*)(p + 8) = F8;
            p += 16;
            ffCount -= 16;
        }

        // 8-byte chunk
        if (ffCount >= 8)
        {
            *(ulong*)p = F8;
            p += 8;
            ffCount -= 8;
        }

        // 4-byte chunk
        if (ffCount >= 4)
        {
            // 0xFFFFFFFF as uint
            *(uint*)p = 0xFFFFFFFFu;
            p += 4;
            ffCount -= 4;
        }

        // Tail: 0..3 single bytes
        switch (ffCount)
        {
            case 3: p[0] = 255; p[1] = 255; p[2] = 255; p += 3; break;
            case 2: p[0] = 255; p[1] = 255; p += 2; break;
            case 1: p[0] = 255; p += 1; break; // 0
            default:
                break;
        }

        // Final remainder (<255)
        *p = last;
        p += 1;

        return (int)(p - dest);
    }

    /// <summary>
    /// Reads a variable-length integer (little-endian).
    /// </summary>
    /// <param name="src"></param>
    /// <param name="srcEnd"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static int ReadVarInt(ref byte* src, byte* srcEnd, out int value)
    {
        value = 0;
        int bytesRead = 0;

        while ((ulong)(srcEnd - src) >= 8)
        {
            if (src[0] != 255)
            {
                break;
            }

            // k = 1
            if (src[1] != 255)
            {
                const int add = 255 * 1;
                if (value > int.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 1; bytesRead += 1; goto Terminate;
            }
            // k = 2
            if (src[2] != 255)
            {
                const int add = 255 * 2;
                if (value > int.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 2; bytesRead += 2; goto Terminate;
            }
            // k = 3
            if (src[3] != 255)
            {
                const int add = 255 * 3;
                if (value > int.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 3; bytesRead += 3; goto Terminate;
            }
            // k = 4
            if (src[4] != 255)
            {
                const int add = 255 * 4;
                if (value > int.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 4; bytesRead += 4; goto Terminate;
            }
            // k = 5
            if (src[5] != 255)
            {
                const int add = 255 * 5;
                if (value > int.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 5; bytesRead += 5; goto Terminate;
            }
            // k = 6
            if (src[6] != 255)
            {
                const int add = 255 * 6;
                if (value > int.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 6; bytesRead += 6; goto Terminate;
            }
            // k = 7
            if (src[7] != 255)
            {
                const int add = 255 * 7;
                if (value > int.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 7; bytesRead += 7; goto Terminate;
            }

            // 8×0xFF nguyên khối
            const int add8 = 255 * 8;
            if (value > int.MaxValue - add8) { value = -1; return bytesRead; }
            value += add8; src += 8; bytesRead += 8;
            continue;

        Terminate:
            break;
        }

        // Đuôi scalar
        while (src < srcEnd && *src == 255)
        {
            if (value > int.MaxValue - 255) { value = -1; return bytesRead; }
            value += 255; src++; bytesRead++;
        }

        // Phải có byte kết thúc (<255)
        if (src >= srcEnd) { value = -1; return bytesRead; }

        int last = *src; // 0..254
        if (value > int.MaxValue - last) { value = -1; return bytesRead; }
        value += last; src++; bytesRead++;

        return bytesRead;
    }
}
