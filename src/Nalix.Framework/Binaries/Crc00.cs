// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Binaries;

/// <summary>
/// Provides methods for generating lookup tables for various CRC algorithms.
/// </summary>
/// <remarks>
/// This utility is used internally by CRC implementations to precompute
/// values for fast checksum calculations.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
internal static class Crc00
{
    /// <summary>
    /// Generates a lookup table for CRC-16 based on the specified polynomial.
    /// </summary>
    /// <param name="poly">The polynomial to use for table generation (e.g., <c>0x8005</c>).</param>
    /// <returns>An array of 256 precomputed CRC-16 values.</returns>
    internal static System.UInt16[] GenerateTable16(System.UInt16 poly)
    {
        var table = new System.UInt16[256];

        for (System.Int16 i = 0; i < 256; i++)
        {
            System.UInt16 crc = (System.UInt16)i;
            for (System.Byte j = 0; j < 8; j++)
            {
                crc = (System.UInt16)((crc & 1) != 0 ? crc >> 1 ^ poly : crc >> 1);
            }

            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Generates a lookup table for CRC-32 based on the specified polynomial.
    /// </summary>
    /// <param name="poly">The polynomial to use for table generation (e.g., <c>0xEDB88320</c>).</param>
    /// <returns>An array of 256 precomputed CRC-32 values.</returns>
    internal static System.UInt32[] GenerateTable32(System.UInt32 poly)
    {
        var table = new System.UInt32[256];

        for (System.UInt32 i = 0; i < 256; i++)
        {
            System.UInt32 crc = i;
            for (System.Byte j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? crc >> 1 ^ poly : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Generates a lookup table for CRC-64 based on the specified polynomial.
    /// </summary>
    /// <param name="poly">The polynomial to use for table generation (e.g., <c>0xC96C5795D7870F42</c>).</param>
    /// <returns>An array of 256 precomputed CRC-64 values.</returns>
    internal static System.UInt64[] GenerateTable64(System.UInt64 poly)
    {
        var table = new System.UInt64[256];

        for (System.Int32 i = 0; i < 256; i++)
        {
            System.UInt64 crc = (System.UInt64)i;
            for (System.Byte j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? crc >> 1 ^ poly : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }
}
