using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Notio.Cryptography.Integrity;

/// <summary>
/// High-performance implementation of CRC16 checksum calculation.
/// </summary>
public static class Crc16
{
    private const ushort Polynomial = 0x8005;
    private const ushort InitialValue = 0xFFFF;

    /// <summary>
    /// Precomputed lookup table for CRC-16/MODBUS polynomial (0x8005).
    /// This table is used to speed up CRC-16 calculations.
    /// </summary>
    private static readonly ushort[] Crc16LookupTable = Crc.GenerateTable16(Polynomial);

    /// <summary>
    /// Calculates the CRC16 for the entire byte array provided.
    /// </summary>
    /// <param name="bytes">The input byte array.</param>
    /// <returns>The CRC16 value as a ushort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Compute(params byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Compute(bytes.AsSpan());
    }

    /// <summary>
    /// Calculates the CRC16 for a chunk of data in a byte array.
    /// </summary>
    /// <param name="bytes">The input byte array.</param>
    /// <param name="start">The index to start processing.</param>
    /// <param name="length">The Number of bytes to process.</param>
    /// <returns>The CRC16 value as a ushort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Compute(byte[] bytes, int start, int length)
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
    /// Computes the CRC16 for a span of bytes with optimized processing.
    /// </summary>
    /// <param name="bytes">Span of input bytes.</param>
    /// <returns>CRC16 value as ushort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Compute(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            throw new ArgumentException("Byte span cannot be empty", nameof(bytes));

        if (Sse42.IsSupported && bytes.Length >= 8) return ComputeSse42(bytes);
        if (Vector.IsHardwareAccelerated && bytes.Length >= 16) return ComputeSimd(bytes);
        else return ComputeScalar(bytes);
    }

    /// <summary>
    /// Computes the CRC16 for any unmanaged generic data type.
    /// </summary>
    /// <typeparam name="T">Any unmanaged data type</typeparam>
    /// <param name="data">The data to compute the CRC16 for</param>
    /// <returns>The CRC16 value as a ushort</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Compute<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        if (data.IsEmpty)
            throw new ArgumentException("Data span cannot be empty", nameof(data));

        // Reinterpret the generic type as a byte span
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(data);
        return Compute(bytes);
    }

    /// <summary>
    /// Verifies if the provided data matches the expected CRC16 value.
    /// </summary>
    /// <param name="data">The data to verify</param>
    /// <param name="expectedCrc">The expected CRC16 value</param>
    /// <returns>True if the CRC matches, otherwise false</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Verify(ReadOnlySpan<byte> data, ushort expectedCrc)
        => Compute(data) == expectedCrc;

    /// <summary>
    /// Processes 8 bytes at once for improved performance on larger inputs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ProcessOctet(ushort crc, ReadOnlySpan<byte> octet)
    {
        ref byte data = ref MemoryMarshal.GetReference(octet);

        crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ data) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 1)) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 2)) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 3)) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 4)) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 5)) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 6)) & 0xFF]);
        crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 7)) & 0xFF]);

        return crc;
    }

    private static ushort ComputeScalar(ReadOnlySpan<byte> bytes)
    {
        ushort crc = InitialValue;

        // Process 8 bytes at once for larger inputs
        if (bytes.Length >= 8)
        {
            int blockCount = bytes.Length / 8;
            int remaining = bytes.Length % 8;

            // Process 8-byte blocks
            for (int i = 0; i < blockCount * 8; i += 8)
            {
                crc = ProcessOctet(crc, bytes.Slice(i, 8));
            }

            // Process remaining bytes
            for (int i = bytes.Length - remaining; i < bytes.Length; i++)
            {
                crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ bytes[i]) & 0xFF]);
            }
        }
        else
        {
            // For small inputs, use the simple loop
            for (int i = 0; i < bytes.Length; i++)
            {
                crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ bytes[i]) & 0xFF]);
            }
        }

        return crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ComputeSse42(ReadOnlySpan<byte> bytes)
    {
        ushort crc = InitialValue;

        if (Sse42.IsSupported)
        {
            ref byte start = ref MemoryMarshal.GetReference(bytes);
            int length = bytes.Length;

            int i = 0;
            for (; i + 8 <= length; i += 8)
            {
                crc = (ushort)Sse42.Crc32(crc, Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref start, i)));
            }

            for (; i < length; i++)
            {
                crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref start, i)) & 0xFF]);
            }
        }
        else
        {
            return ComputeScalar(bytes);
        }

        return crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ComputeSimd(ReadOnlySpan<byte> bytes)
    {
        ushort crc = InitialValue;
        int vectorSize = Vector<byte>.Count;
        int vectorCount = bytes.Length / vectorSize;

        if (vectorCount > 0)
        {
            ref byte start = ref MemoryMarshal.GetReference(bytes);
            int i = 0;

            for (; i < vectorCount * vectorSize; i += vectorSize)
            {
                Vector<byte> vec = Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.Add(ref start, i));
                crc = ProcessVector(crc, vec);
            }

            for (; i < bytes.Length; i++)
            {
                crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref start, i)) & 0xFF]);
            }
        }
        else
        {
            return ComputeScalar(bytes);
        }

        return crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ProcessVector(ushort crc, Vector<byte> vec)
    {
        for (int i = 0; i < Vector<byte>.Count; i++)
        {
            crc = (ushort)((crc >> 8) ^ Crc16LookupTable[(crc ^ vec[i]) & 0xFF]);
        }
        return crc;
    }
}
