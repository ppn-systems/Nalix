// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.LZ4.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.LZ4.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Internal;

/// <summary>
/// Helper methods for working with Spans.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static unsafe class SpanOps
{
    /// <summary>
    /// Writes a variable-length integer (little-endian). Used for lengths greater than 15.
    /// Writes bytes until the value is less than 255.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 WriteVarInt(System.Byte* dest, System.Int32 value)
    {
        // Negative should never happen in encoder paths; clamp-to-0 preserves protocol.
        if (value < 0)
        {
            System.Diagnostics.Debug.Fail("WriteVarInt: negative value.");
            value = 0;
        }

        // Number of full 0xFF bytes and the final remainder (<255)
        // This removes the loop-carried subtraction by 255.
        System.UInt32 u = (System.UInt32)value;
        System.Int32 ffCount = (System.Int32)(u / 255u);     // how many 0xFF
        System.Byte last = (System.Byte)(u % 255u);    // final terminator

        System.Byte* p = dest;

        // Write 0xFF blocks with wide stores to reduce loop overhead
        const System.UInt64 F8 = 0xFFFFFFFFFFFFFFFFul;

        // 16-byte chunks (2x ulong)
        while (ffCount >= 16)
        {
            // write 16 bytes of 0xFF
            *(System.UInt64*)p = F8; *(System.UInt64*)(p + 8) = F8;
            p += 16;
            ffCount -= 16;
        }

        // 8-byte chunk
        if (ffCount >= 8)
        {
            *(System.UInt64*)p = F8;
            p += 8;
            ffCount -= 8;
        }

        // 4-byte chunk
        if (ffCount >= 4)
        {
            // 0xFFFFFFFF as uint
            *(System.UInt32*)p = 0xFFFFFFFFu;
            p += 4;
            ffCount -= 4;
        }

        // Tail: 0..3 single bytes
        switch (ffCount)
        {
            case 3: p[0] = 255; p[1] = 255; p[2] = 255; p += 3; break;
            case 2: p[0] = 255; p[1] = 255; p += 2; break;
            case 1: p[0] = 255; p += 1; break;
            default: break; // 0
        }

        // Final remainder (<255)
        *p = last;
        p += 1;

        return (System.Int32)(p - dest);
    }

    /// <summary>
    /// Reads a variable-length integer (little-endian).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1089:Use --/++ operator instead of assignment", Justification = "<Pending>")]
    public static System.Int32 ReadVarInt(ref System.Byte* src, System.Byte* srcEnd, out System.Int32 value)
    {
        value = 0;
        System.Int32 bytesRead = 0;

        while ((System.UInt64)(srcEnd - src) >= 8)
        {
            if (src[0] != 255)
            {
                break;
            }

            // k = 1
            if (src[1] != 255)
            {
                const System.Int32 add = 255 * 1;
                if (value > System.Int32.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 1; bytesRead += 1; goto Terminate;
            }
            // k = 2
            if (src[2] != 255)
            {
                const System.Int32 add = 255 * 2;
                if (value > System.Int32.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 2; bytesRead += 2; goto Terminate;
            }
            // k = 3
            if (src[3] != 255)
            {
                const System.Int32 add = 255 * 3;
                if (value > System.Int32.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 3; bytesRead += 3; goto Terminate;
            }
            // k = 4
            if (src[4] != 255)
            {
                const System.Int32 add = 255 * 4;
                if (value > System.Int32.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 4; bytesRead += 4; goto Terminate;
            }
            // k = 5
            if (src[5] != 255)
            {
                const System.Int32 add = 255 * 5;
                if (value > System.Int32.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 5; bytesRead += 5; goto Terminate;
            }
            // k = 6
            if (src[6] != 255)
            {
                const System.Int32 add = 255 * 6;
                if (value > System.Int32.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 6; bytesRead += 6; goto Terminate;
            }
            // k = 7
            if (src[7] != 255)
            {
                const System.Int32 add = 255 * 7;
                if (value > System.Int32.MaxValue - add) { value = -1; return bytesRead; }
                value += add; src += 7; bytesRead += 7; goto Terminate;
            }

            // 8×0xFF nguyên khối
            const System.Int32 add8 = 255 * 8;
            if (value > System.Int32.MaxValue - add8) { value = -1; return bytesRead; }
            value += add8; src += 8; bytesRead += 8;
            continue;

        Terminate:
            break;
        }

        // Đuôi scalar
        while (src < srcEnd && *src == 255)
        {
            if (value > System.Int32.MaxValue - 255) { value = -1; return bytesRead; }
            value += 255; src++; bytesRead++;
        }

        // Phải có byte kết thúc (<255)
        if (src >= srcEnd) { value = -1; return bytesRead; }

        System.Int32 last = *src; // 0..254
        if (value > System.Int32.MaxValue - last) { value = -1; return bytesRead; }
        value += last; src++; bytesRead++;

        return bytesRead;
    }
}
