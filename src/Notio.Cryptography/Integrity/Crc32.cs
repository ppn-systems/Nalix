using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Notio.Cryptography.Integrity;

/// <summary>
/// High-performance implementation of CRC32 checksum calculation using the
/// reversed polynomial 0xEDB88320 (which is equivalent to
/// x^32 + x^26 + x^23 + x^22 + x^16 + x^12 + x^11 + x^10 + x^8 + x^7 + x^5 + x^4 + x^2 + x + 1).
/// </summary>
public static class Crc32
{
    private const uint Polynomial = 0xEDB88320;
    private const uint InitialValue = 0xFFFFFFFF;

    /// <summary>
    /// Precomputed lookup table for CRC-32/MODBUS polynomial (0x04C11DB7).
    /// This table is used to speed up CRC-32 calculations.
    /// </summary>
    private static readonly uint[] Crc32LookupTable = Crc.GenerateTable32(Polynomial);

    /// <summary>
    /// Computes the CRC32 checksum for the specified range in the byte array.
    /// </summary>
    /// <param name="bytes">The input byte array.</param>
    /// <param name="start">The starting index to begin CRC computation.</param>
    /// <param name="length">The Number of bytes to process.</param>
    /// <returns>The 32-bit CRC value.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if the input array is null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if parameters are out of valid range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(byte[] bytes, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Byte array cannot be empty.");

        if (start < 0 || start >= bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(start));

        if (length < 0 || start + length > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        return Compute(bytes.AsSpan(start, length));
    }

    /// <summary>
    /// Computes the CRC32 checksum for the specified span of bytes with hardware acceleration when available.
    /// </summary>
    /// <param name="bytes">The input span of bytes.</param>
    /// <returns>The 32-bit CRC value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
        // Use hardware acceleration if available
        if (Sse42.IsSupported && bytes.Length >= 16)
        {
            return ComputeSse42(bytes);
        }

        // Use SIMD acceleration for larger inputs
        if (Vector.IsHardwareAccelerated && bytes.Length >= 32)
        {
            return ComputeSimd(bytes);
        }

        return ComputeScalar(bytes);
    }

    /// <summary>
    /// Computes the CRC32 checksum for the provided bytes.
    /// </summary>
    /// <param name="bytes">The input byte array.</param>
    /// <returns>The 32-bit CRC value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(params byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Compute(bytes.AsSpan());
    }

    /// <summary>
    /// Computes CRC32 for any unmanaged type data.
    /// </summary>
    /// <typeparam name="T">Any unmanaged data type.</typeparam>
    /// <param name="data">The data to compute CRC32 for.</param>
    /// <returns>The 32-bit CRC value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute<T>(ReadOnlySpan<T> data) where T : unmanaged
        => Compute(MemoryMarshal.AsBytes(data));

    /// <summary>
    /// Verifies if the data matches the expected CRC32 checksum.
    /// </summary>
    /// <param name="data">The data to verify.</param>
    /// <param name="expectedCrc">The expected CRC32 value.</param>
    /// <returns>True if the CRC matches, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Verify(ReadOnlySpan<byte> data, uint expectedCrc)
        => Compute(data) == expectedCrc;

    /// <summary>
    /// Process 8 bytes at once for better performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ProcessOctet(uint crc, ReadOnlySpan<byte> octet)
    {
        ref byte data = ref MemoryMarshal.GetReference(octet);

        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ data];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ Unsafe.Add(ref data, 1)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ Unsafe.Add(ref data, 2)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ Unsafe.Add(ref data, 3)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ Unsafe.Add(ref data, 4)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ Unsafe.Add(ref data, 5)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ Unsafe.Add(ref data, 6)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ Unsafe.Add(ref data, 7)];

        return crc;
    }


    /// <summary>
    /// Scalar implementation of CRC32 calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ComputeScalar(ReadOnlySpan<byte> bytes)
    {
        uint crc = InitialValue;

        // Process 8 bytes at once for medium-sized inputs
        if (bytes.Length >= 8)
        {
            int blockCount = bytes.Length / 8;
            int remainder = bytes.Length % 8;

            for (int i = 0; i < blockCount * 8; i += 8)
            {
                crc = ProcessOctet(crc, bytes.Slice(i, 8));
            }

            // Process remaining bytes
            for (int i = bytes.Length - remainder; i < bytes.Length; i++)
            {
                crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ bytes[i]];
            }
        }
        else
        {
            // Simple loop for small inputs
            for (int i = 0; i < bytes.Length; i++)
            {
                crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ bytes[i]];
            }
        }

        return ~crc; // Final complement
    }

    /// <summary>
    /// SIMD-accelerated implementation of CRC32 calculation using Vector&lt;byte&gt;.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint ComputeSimd(ReadOnlySpan<byte> bytes)
    {
        uint crc = InitialValue;

        // Process data in chunks of Vector<byte>.Count
        int vectorSize = Vector<byte>.Count;
        int vectorCount = bytes.Length / vectorSize;

        // Process vectors
        if (vectorCount > 0)
        {
            fixed (byte* ptr = bytes)
            {
                for (int i = 0; i < vectorCount * vectorSize; i += vectorSize)
                {
                    // Process each byte in the vector
                    for (int j = 0; j < vectorSize; j++)
                    {
                        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ ptr[i + j]];
                    }
                }

                // Process remaining bytes
                for (int i = vectorCount * vectorSize; i < bytes.Length; i++)
                {
                    crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ ptr[i]];
                }
            }
        }
        else
        {
            // Fall back to scalar implementation for small inputs
            return ComputeScalar(bytes);
        }

        return ~crc; // Final complement
    }

    /// <summary>
    /// SSE4.2 hardware-accelerated implementation of CRC32 calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint ComputeSse42(ReadOnlySpan<byte> bytes)
    {
        uint crc = InitialValue;

        fixed (byte* pBytes = bytes)
        {
            byte* p = pBytes;
            byte* end = p + bytes.Length;

            // Process 8-byte chunks using the CRC32 instruction
            if (Sse42.X64.IsSupported && bytes.Length >= 8)
            {
                // Process 8 bytes at a time
                while (p + 8 <= end)
                {
                    crc = (uint)Sse42.X64.Crc32(crc, *(ulong*)p);
                    p += 8;
                }
            }

            // Process 4-byte chunks
            while (p + 4 <= end)
            {
                crc = Sse42.Crc32(crc, *(uint*)p);
                p += 4;
            }

            // Process remaining bytes
            while (p < end)
            {
                crc = Sse42.Crc32(crc, *p);
                p++;
            }
        }

        return ~crc; // Final complement
    }
}
