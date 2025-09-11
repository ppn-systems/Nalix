// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Binaries;

/// <summary>
/// Provides high-performance methods for calculating CRC-16 checksums using the MODBUS polynomial (0x8005).
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
public static class Crc16
{
    #region Const

    /// <summary>
    /// The polynomial used for CRC-16/MODBUS calculations (0x8005).
    /// </summary>
    private const System.UInt16 Polynomial = 0x8005;

    /// <summary>
    /// The initial value for CRC-16 calculations (0xFFFF).
    /// </summary>
    private const System.UInt16 InitialValue = 0xFFFF;

    #endregion Const

    #region Fields

    /// <summary>
    /// Precomputed lookup table for CRC-16/MODBUS polynomial (0x8005).
    /// This table optimizes CRC-16 calculations by precomputing results for all possible byte values.
    /// </summary>
    private static readonly System.UInt16[] Crc16LookupTable = Crc00.GenerateTable16(Polynomial);

    #endregion Fields

    #region APIs

    /// <summary>
    /// Computes the CRC-16 checksum for the specified byte array.
    /// </summary>
    /// <param name="bytes">The byte array to compute the CRC-16 checksum for.</param>
    /// <returns>The computed CRC-16 checksum as a 16-bit unsigned integer.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="bytes"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="bytes"/> is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 Compute(params System.Byte[] bytes)
    {
        System.ArgumentNullException.ThrowIfNull(bytes);
        return Compute(System.MemoryExtensions.AsSpan(bytes));
    }

    /// <summary>
    /// Computes the CRC-16 checksum for a specified range of bytes in the provided byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the data to compute the CRC-16 checksum for.</param>
    /// <param name="start">The starting index in the byte array.</param>
    /// <param name="length">The number of bytes to process.</param>
    /// <returns>The computed CRC-16 checksum as a 16-bit unsigned integer.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="bytes"/> is null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="start"/> or <paramref name="length"/> is out of range, or the byte array is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 Compute(System.Byte[] bytes, System.Int32 start, System.Int32 length)
    {
        System.ArgumentNullException.ThrowIfNull(bytes);

        return bytes.Length == 0
            ? throw new System.ArgumentOutOfRangeException(nameof(bytes), "Byte array cannot be empty.")
            : start < 0 || start >= bytes.Length
            ? throw new System.ArgumentOutOfRangeException(nameof(start))
            : length < 0 || start + length > bytes.Length
            ? throw new System.ArgumentOutOfRangeException(nameof(length))
            : Compute(System.MemoryExtensions.AsSpan(bytes, start, length));
    }

    /// <summary>
    /// Computes the CRC-16 checksum for a read-only span of bytes.
    /// </summary>
    /// <param name="bytes">The read-only span of bytes to compute the CRC-16 checksum for.</param>
    /// <returns>The computed CRC-16 checksum as a 16-bit unsigned integer.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="bytes"/> is empty.</exception>
    /// <remarks>
    /// This method selects the optimal computation method based on hardware support:
    /// SSE4.2 for lengths >= 8 bytes, SIMD for lengths >= 16 bytes, or scalar computation otherwise.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 Compute(System.ReadOnlySpan<System.Byte> bytes)
    {
        return bytes.IsEmpty
            ? throw new System.ArgumentException("Byte span cannot be empty", nameof(bytes))
            : System.Runtime.Intrinsics.X86.Sse42.IsSupported && bytes.Length >= 8
            ? ComputeSse42(bytes)
            : System.Numerics.Vector.IsHardwareAccelerated && bytes.Length >= 16
            ? ComputeSimd(bytes)
            : ComputeScalar(bytes);
    }

    /// <summary>
    /// Computes the CRC-16 checksum for a read-only span of unmanaged data.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the data.</typeparam>
    /// <param name="data">The read-only span of unmanaged data to compute the CRC-16 checksum for.</param>
    /// <returns>The computed CRC-16 checksum as a 16-bit unsigned integer.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 Compute<T>(System.ReadOnlySpan<T> data) where T : unmanaged
    {
        if (data.IsEmpty)
        {
            throw new System.ArgumentException("Data span cannot be empty", nameof(data));
        }

        System.ReadOnlySpan<System.Byte> bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data);
        return Compute(bytes);
    }

    /// <summary>
    /// Verifies whether the computed CRC-16 checksum matches the expected checksum.
    /// </summary>
    /// <param name="data">The read-only span of bytes to verify.</param>
    /// <param name="expectedCrc">The expected CRC-16 checksum to compare against.</param>
    /// <returns><c>true</c> if the computed CRC-16 checksum matches <paramref name="expectedCrc"/>; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Verify(System.ReadOnlySpan<System.Byte> data, System.UInt16 expectedCrc)
        => Compute(data) == expectedCrc;

    #endregion APIs

    #region Lookup Table Generation

    /// <summary>
    /// Processes an octet (8 bytes) of data to update the CRC-16 checksum using the lookup table.
    /// </summary>
    /// <param name="crc">The current CRC-16 checksum value.</param>
    /// <param name="octet">The read-only span of 8 bytes to process.</param>
    /// <returns>The updated CRC-16 checksum after processing the octet.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 ProcessOctet(System.UInt16 crc, System.ReadOnlySpan<System.Byte> octet)
    {
        ref System.Byte data = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(octet);

        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ data) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 1)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 2)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 3)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 4)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 5)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 6)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 7)) & 0xFF]);

        return crc;
    }

    /// <summary>
    /// Computes the CRC-16 checksum using a scalar (non-hardware-accelerated) approach.
    /// </summary>
    /// <param name="bytes">The read-only span of bytes to compute the CRC-16 checksum for.</param>
    /// <returns>The computed CRC-16 checksum as a 16-bit unsigned integer.</returns>
    private static System.UInt16 ComputeScalar(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt16 crc = InitialValue;

        if (bytes.Length >= 8)
        {
            System.Int32 blockCount = bytes.Length / 8;
            System.Int32 remaining = bytes.Length % 8;

            for (System.Int32 i = 0; i < blockCount * 8; i += 8)
            {
                crc = ProcessOctet(crc, bytes.Slice(i, 8));
            }

            for (System.Int32 i = bytes.Length - remaining; i < bytes.Length; i++)
            {
                crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ bytes[i]) & 0xFF]);
            }
        }
        else
        {
            for (System.Int32 i = 0; i < bytes.Length; i++)
            {
                crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ bytes[i]) & 0xFF]);
            }
        }

        return crc;
    }

    /// <summary>
    /// Computes the CRC-16 checksum using SSE4.2 hardware acceleration when available.
    /// </summary>
    /// <param name="bytes">The read-only span of bytes to compute the CRC-16 checksum for.</param>
    /// <returns>The computed CRC-16 checksum as a 16-bit unsigned integer.</returns>
    /// <remarks>
    /// Falls back to <see cref="ComputeScalar(System.ReadOnlySpan{System.Byte})"/> if SSE4.2 is not supported.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 ComputeSse42(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt16 crc = InitialValue;

        if (System.Runtime.Intrinsics.X86.Sse42.IsSupported)
        {
            ref System.Byte start = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(bytes);
            System.Int32 length = bytes.Length;

            System.Int32 i = 0;
            for (; i + 8 <= length; i += 8)
            {
                crc = (System.UInt16)System.Runtime.Intrinsics.X86.Sse42.Crc32(crc, System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(ref System.Runtime.CompilerServices.Unsafe.Add(ref start, i)));
            }

            for (; i < length; i++)
            {
                crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref start, i)) & 0xFF]);
            }
        }
        else
        {
            return ComputeScalar(bytes);
        }

        return crc;
    }

    /// <summary>
    /// Computes the CRC-16 checksum using SIMD hardware acceleration when available.
    /// </summary>
    /// <param name="bytes">The read-only span of bytes to compute the CRC-16 checksum for.</param>
    /// <returns>The computed CRC-16 checksum as a 16-bit unsigned integer.</returns>
    /// <remarks>
    /// Falls back to <see cref="ComputeScalar(System.ReadOnlySpan{System.Byte})"/> if the data size is too small for SIMD processing.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 ComputeSimd(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt16 crc = InitialValue;
        System.Int32 vectorSize = System.Numerics.Vector<System.Byte>.Count;
        System.Int32 vectorCount = bytes.Length / vectorSize;

        if (vectorCount > 0)
        {
            ref System.Byte start = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(bytes);
            System.Int32 i = 0;

            for (; i < vectorCount * vectorSize; i += vectorSize)
            {
                System.Numerics.Vector<System.Byte> vec = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Numerics.Vector<System.Byte>>(ref System.Runtime.CompilerServices.Unsafe.Add(ref start, i));
                crc = ProcessVector(crc, vec);
            }

            for (; i < bytes.Length; i++)
            {
                crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref start, i)) & 0xFF]);
            }
        }
        else
        {
            return ComputeScalar(bytes);
        }

        return crc;
    }

    /// <summary>
    /// Processes a vector of bytes to update the CRC-16 checksum using SIMD operations.
    /// </summary>
    /// <param name="crc">The current CRC-16 checksum value.</param>
    /// <param name="vec">The vector of bytes to process.</param>
    /// <returns>The updated CRC-16 checksum after processing the vector.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 ProcessVector(System.UInt16 crc, System.Numerics.Vector<System.Byte> vec)
    {
        for (System.Int32 i = 0; i < System.Numerics.Vector<System.Byte>.Count; i++)
        {
            crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ vec[i]) & 0xFF]);
        }
        return crc;
    }

    #endregion Lookup Table Generation
}