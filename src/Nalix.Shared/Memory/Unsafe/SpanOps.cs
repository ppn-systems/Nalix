// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Unsafe;

/// <summary>
/// Helper methods for working with Spans.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static unsafe class SpanOps
{
    /// <summary>
    /// Ensures the requested slice is within the span bounds.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void CheckSliceBounds(System.Int32 length, System.Int32 start, System.Int32 count)
    {
        // Use unsigned arithmetic for efficient check: (uint)start + (uint)count > (uint)length
        if ((System.UInt32)start > (System.UInt32)length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(start));
        }

        if ((System.UInt32)count > (System.UInt32)(length - start))
        {
            throw new System.ArgumentOutOfRangeException(nameof(count));
        }
    }

    /// <summary>
    /// Writes a variable-length integer (little-endian). Used for lengths greater than 15.
    /// Writes bytes until the value is less than 255.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 WriteVarInt(System.Byte* dest, System.Int32 value)
    {
        System.Int32 bytesWritten = 0;
        System.UInt32 uValue = (System.UInt32)value;

        while (uValue >= 255)
        {
            *dest++ = 255;
            uValue -= 255;
            bytesWritten++;
        }

        *dest = (System.Byte)uValue;
        return bytesWritten + 1;
    }

    /// <summary>
    /// Reads a variable-length integer (little-endian).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 ReadVarInt(
        ref System.Byte* src,
        System.Byte* srcEnd,
        out System.Int32 value)
    {
        value = 0;
        System.Int32 bytesRead = 0;
        System.Byte currentByte;

        while (src < srcEnd)
        {
            currentByte = *src;
            src++;
            value += currentByte;
            bytesRead++;

            if (currentByte < 255)
            {
                return bytesRead;
            }
        }

        value = -1; // Error: reached end without termination
        return bytesRead;
    }
}
