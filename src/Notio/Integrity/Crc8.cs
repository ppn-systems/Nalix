using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Notio.Integrity;

/// <summary>
/// A high-performance CRC-8 implementation using polynomial x^8 + x^7 + x^6 + x^4 + x^2 + 1
/// </summary>
public static class Crc8
{
    private const byte Polynomial = 0x31;
    private const byte InitialValue = 0xFF;

    /// <summary>
    /// Precomputed lookup table for CRC-8/MODBUS polynomial (0x31).
    /// This table is used to speed up CRC-8 calculations.
    /// </summary>
    private static readonly byte[] Crc8LookupTable = Crc.GenerateTable8(Polynomial);

    /// <summary>
    /// Computes the CRC-8 checksum of the specified bytes
    /// </summary>
    /// <param name="bytes">The buffer to compute the CRC upon</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Compute(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) throw new ArgumentException("Bytes span cannot be empty", nameof(bytes));
        if (Sse42.IsSupported && bytes.Length >= 16) return ComputeSse42(bytes);
        if (Vector.IsHardwareAccelerated && bytes.Length >= 32) return ComputeSimd(bytes);
        else return ComputeScalar(bytes);
    }

    /// <summary>
    /// Computes the CRC-8 checksum of the specified bytes
    /// </summary>
    /// <param name="bytes">The buffer to compute the CRC upon</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Compute(params byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            throw new ArgumentException("Bytes array cannot be null or empty", nameof(bytes));

        return Compute(bytes.AsSpan());
    }

    /// <summary>
    /// Computes the CRC-8 of the specified byte range
    /// </summary>
    /// <param name="bytes">The buffer to compute the CRC upon</param>
    /// <param name="start">The start index upon which to compute the CRC</param>
    /// <param name="length">The length of the buffer upon which to compute the CRC</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Compute(byte[] bytes, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (bytes.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Bytes array cannot be empty");

        if (start >= bytes.Length && length > 1)
            throw new ArgumentOutOfRangeException(nameof(start), "Start index is out of range");

        int end = start + length;
        if (end > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(length), "Specified length exceeds buffer bounds");

        return Compute(bytes.AsSpan(start, length));
    }

    /// <summary>
    /// Computes the CRC-8 of the specified memory
    /// </summary>
    /// <param name="data">The memory to compute the CRC upon</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe byte Compute<T>(Span<T> data) where T : unmanaged
    {
        if (data.IsEmpty)
            throw new ArgumentException("Data span cannot be empty", nameof(data));

        ReadOnlySpan<byte> bytes;
        if (typeof(T) == typeof(byte))
        {
            bytes = MemoryMarshal.Cast<T, byte>(data);
        }
        else
        {
            // Handle non-byte spans by reinterpreting as bytes
            bytes = MemoryMarshal.AsBytes(data);
        }

        return Compute(bytes);
    }

    /// <summary>
    /// Verifies if the data matches the expected CRC-8 checksum.
    /// </summary>
    /// <param name="data">The data to verify.</param>
    /// <param name="expectedCrc">The expected CRC-8 value.</param>
    /// <returns>True if the CRC matches, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Verify(ReadOnlySpan<byte> data, byte expectedCrc)
        => Compute(data) == expectedCrc;

    /// <summary>
    /// Process 8 bytes at a time for better performance on larger inputs
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ProcessOctet(byte crc, ReadOnlySpan<byte> octet)
    {
        ref byte data = ref MemoryMarshal.GetReference(octet);

        crc = Crc8LookupTable[crc ^ data];
        crc = Crc8LookupTable[crc ^ Unsafe.Add(ref data, 1)];
        crc = Crc8LookupTable[crc ^ Unsafe.Add(ref data, 2)];
        crc = Crc8LookupTable[crc ^ Unsafe.Add(ref data, 3)];
        crc = Crc8LookupTable[crc ^ Unsafe.Add(ref data, 4)];
        crc = Crc8LookupTable[crc ^ Unsafe.Add(ref data, 5)];
        crc = Crc8LookupTable[crc ^ Unsafe.Add(ref data, 6)];
        crc = Crc8LookupTable[crc ^ Unsafe.Add(ref data, 7)];

        return crc;
    }

    /// <summary>
    /// Computes the CRC-8 checksum of the specified bytes
    /// </summary>
    /// <param name="bytes">The buffer to compute the CRC upon</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ComputeScalar(ReadOnlySpan<byte> bytes)
    {
        byte crc = InitialValue;

        // Process bytes in chunks when possible
        if (bytes.Length >= 8)
        {
            int unalignedBytes = bytes.Length % 8;
            int alignedLength = bytes.Length - unalignedBytes;

            for (int i = 0; i < alignedLength; i += 8)
            {
                crc = ProcessOctet(crc, bytes.Slice(i, 8));
            }

            // Process remaining bytes
            for (int i = alignedLength; i < bytes.Length; i++)
            {
                crc = Crc8LookupTable[crc ^ bytes[i]];
            }
        }
        else
        {
            // Process small arrays with simple loop
            for (int i = 0; i < bytes.Length; i++)
            {
                crc = Crc8LookupTable[crc ^ bytes[i]];
            }
        }

        return crc;
    }

    /// <summary>
    /// SIMD-accelerated implementation of CRC-8.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ComputeSimd(ReadOnlySpan<byte> bytes)
    {
        byte crc = InitialValue;
        int vectorSize = Vector<byte>.Count;
        int length = bytes.Length;

        int i = 0;
        while (i + vectorSize <= length)
        {
            Vector<byte> dataVec = new(bytes.Slice(i, vectorSize));
            for (int j = 0; j < vectorSize; j++)
            {
                crc = Crc8LookupTable[crc ^ dataVec[j]];
            }
            i += vectorSize;
        }

        for (; i < length; i++) crc = Crc8LookupTable[crc ^ bytes[i]];

        return crc;
    }

    /// <summary>
    /// SSE4.2 accelerated CRC-8 computation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe byte ComputeSse42(ReadOnlySpan<byte> bytes)
    {
        byte crc = InitialValue;

        fixed (byte* pBytes = bytes)
        {
            byte* p = pBytes;
            byte* end = p + bytes.Length;

            while (p < end)
            {
                crc = (byte)Sse42.Crc32(crc, *p);
                p++;
            }
        }

        return crc;
    }

}
