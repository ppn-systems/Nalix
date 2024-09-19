using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Nalix.Cryptography.Checksums;

/// <summary>
/// A high-performance CRC-08 implementation using polynomial x^8 + x^7 + x^6 + x^4 + x^2 + 1
/// </summary>
public static class Crc08
{
    private const System.Byte Polynomial = 0x31;
    private const System.Byte InitialValue = 0xFF;

    /// <summary>
    /// Precomputed lookup table for CRC-8/MODBUS polynomial (0x31).
    /// This table is used to speed up CRC-8 calculations.
    /// </summary>
    private static readonly System.Byte[] Crc8LookupTable = Crc00.GenerateTable8(Polynomial);

    /// <summary>
    /// Computes the CRC-8 checksum of the specified bytes
    /// </summary>
    /// <param name="bytes">The buffer to compute the CRC upon</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute(System.ReadOnlySpan<System.Byte> bytes)
    {
        if (bytes.IsEmpty) throw new System.ArgumentException("Bytes span cannot be empty", nameof(bytes));
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
    public static System.Byte Compute(params System.Byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            throw new System.ArgumentException("Bytes array cannot be null or empty", nameof(bytes));

        return Compute(System.MemoryExtensions.AsSpan(bytes));
    }

    /// <summary>
    /// Computes the CRC-8 of the specified byte range
    /// </summary>
    /// <param name="bytes">The buffer to compute the CRC upon</param>
    /// <param name="start">The start index upon which to compute the CRC</param>
    /// <param name="length">The length of the buffer upon which to compute the CRC</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute(System.Byte[] bytes, System.Int32 start, System.Int32 length)
    {
        System.ArgumentNullException.ThrowIfNull(bytes);
        System.ArgumentOutOfRangeException.ThrowIfNegative(start);
        System.ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (bytes.Length == 0)
            throw new System.ArgumentOutOfRangeException(nameof(bytes), "Bytes array cannot be empty");

        if (start >= bytes.Length && length > 1)
            throw new System.ArgumentOutOfRangeException(nameof(start), "Start index is out of range");

        System.Int32 end = start + length;
        if (end > bytes.Length)
            throw new System.ArgumentOutOfRangeException(nameof(length), "Specified length exceeds buffer bounds");

        return Compute(System.MemoryExtensions.AsSpan(bytes, start, length));
    }

    /// <summary>
    /// Computes the CRC-8 of the specified memory
    /// </summary>
    /// <param name="data">The memory to compute the CRC upon</param>
    /// <returns>The specified CRC</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Byte Compute<T>(System.Span<T> data) where T : unmanaged
    {
        if (data.IsEmpty)
            throw new System.ArgumentException("Data span cannot be empty", nameof(data));

        System.ReadOnlySpan<System.Byte> bytes;
        if (typeof(T) == typeof(System.Byte))
        {
            bytes = MemoryMarshal.Cast<T, System.Byte>(data);
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
    public static System.Boolean Verify(System.ReadOnlySpan<System.Byte> data, System.Byte expectedCrc)
        => Compute(data) == expectedCrc;

    /// <summary>
    /// Process 8 bytes at a time for better performance on larger inputs
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static System.Byte ProcessOctet(System.Byte crc, System.ReadOnlySpan<System.Byte> octet)
    {
        ref System.Byte data = ref MemoryMarshal.GetReference(octet);

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
    private static System.Byte ComputeScalar(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.Byte crc = InitialValue;

        // Process bytes in chunks when possible
        if (bytes.Length >= 8)
        {
            System.Int32 unalignedBytes = bytes.Length % 8;
            System.Int32 alignedLength = bytes.Length - unalignedBytes;

            for (System.Int32 i = 0; i < alignedLength; i += 8)
            {
                crc = ProcessOctet(crc, bytes.Slice(i, 8));
            }

            // Process remaining bytes
            for (System.Int32 i = alignedLength; i < bytes.Length; i++)
            {
                crc = Crc8LookupTable[crc ^ bytes[i]];
            }
        }
        else
        {
            // Process small arrays with simple loop
            for (System.Int32 i = 0; i < bytes.Length; i++)
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
    private static System.Byte ComputeSimd(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.Byte crc = InitialValue;
        System.Int32 vectorSize = Vector<System.Byte>.Count;
        System.Int32 length = bytes.Length;

        System.Int32 i = 0;
        while (i + vectorSize <= length)
        {
            Vector<System.Byte> dataVec = new(bytes.Slice(i, vectorSize));
            for (System.Int32 j = 0; j < vectorSize; j++)
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
    private static unsafe System.Byte ComputeSse42(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.Byte crc = InitialValue;

        fixed (System.Byte* pBytes = bytes)
        {
            System.Byte* p = pBytes;
            System.Byte* end = p + bytes.Length;

            while (p < end)
            {
                crc = (System.Byte)Sse42.Crc32(crc, *p);
                p++;
            }
        }

        return crc;
    }
}