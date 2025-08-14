// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Nalix.Framework.Checksums;

/// <summary>
/// High-performance implementation of CRC16 checksum calculation.
/// </summary>
public static class Crc16
{
    #region Const

    private const System.UInt16 Polynomial = 0x8005;
    private const System.UInt16 InitialValue = 0xFFFF;

    #endregion Const

    #region Fields

    /// <summary>
    /// Precomputed lookup table for CRC-16/MODBUS polynomial (0x8005).
    /// This table is used to speed up CRC-16 calculations.
    /// </summary>
    private static readonly System.UInt16[] Crc16LookupTable = Crc00.GenerateTable16(Polynomial);

    #endregion Fields

    #region APIs

    /// <summary>
    /// Calculates the CRC16 for the entire byte array provided.
    /// </summary>
    /// <param name="bytes">The input byte array.</param>
    /// <returns>The CRC16 value as a ushort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 Compute(params System.Byte[] bytes)
    {
        System.ArgumentNullException.ThrowIfNull(bytes);
        return Compute(System.MemoryExtensions.AsSpan(bytes));
    }

    /// <summary>
    /// Calculates the CRC16 for a chunk of data in a byte array.
    /// </summary>
    /// <param name="bytes">The input byte array.</param>
    /// <param name="start">The index to start processing.</param>
    /// <param name="length">The TransportProtocol of bytes to process.</param>
    /// <returns>The CRC16 value as a ushort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 Compute(System.Byte[] bytes, System.Int32 start, System.Int32 length)
    {
        System.ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(bytes), "Byte array cannot be empty.");
        }

        if (start < 0 || start >= bytes.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(start));
        }

        return length < 0 || start + length > bytes.Length
            ? throw new System.ArgumentOutOfRangeException(nameof(length))
            : Compute(System.MemoryExtensions.AsSpan(bytes, start, length));
    }

    /// <summary>
    /// Computes the CRC16 for a span of bytes with optimized processing.
    /// </summary>
    /// <param name="bytes">Span of input bytes.</param>
    /// <returns>CRC16 value as ushort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 Compute(System.ReadOnlySpan<System.Byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new System.ArgumentException("Byte span cannot be empty", nameof(bytes));
        }

        if (Sse42.IsSupported && bytes.Length >= 8)
        {
            return ComputeSse42(bytes);
        }

        return Vector.IsHardwareAccelerated && bytes.Length >= 16 ? ComputeSimd(bytes) : ComputeScalar(bytes);
    }

    /// <summary>
    /// Computes the CRC16 for any unmanaged generic data type.
    /// </summary>
    /// <typeparam name="T">Any unmanaged data type</typeparam>
    /// <param name="data">The data to compute the CRC16 for</param>
    /// <returns>The CRC16 value as a ushort</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 Compute<T>(System.ReadOnlySpan<T> data) where T : unmanaged
    {
        if (data.IsEmpty)
        {
            throw new System.ArgumentException("Data span cannot be empty", nameof(data));
        }

        // Reinterpret the generic type as a byte span
        System.ReadOnlySpan<System.Byte> bytes = MemoryMarshal.AsBytes(data);
        return Compute(bytes);
    }

    /// <summary>
    /// Verifies if the provided data matches the expected CRC16 value.
    /// </summary>
    /// <param name="data">The data to verify</param>
    /// <param name="expectedCrc">The expected CRC16 value</param>
    /// <returns>True if the CRC matches, otherwise false</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Verify(System.ReadOnlySpan<System.Byte> data, System.UInt16 expectedCrc)
        => Compute(data) == expectedCrc;

    #endregion APIs

    #region Lookup Table Generation

    /// <summary>
    /// Processes 8 bytes at once for improved performance on larger inputs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 ProcessOctet(
        System.UInt16 crc,
        System.ReadOnlySpan<System.Byte> octet)
    {
        ref System.Byte data = ref MemoryMarshal.GetReference(octet);

        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ data) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 1)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 2)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 3)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 4)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 5)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 6)) & 0xFF]);
        crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref data, 7)) & 0xFF]);

        return crc;
    }

    private static System.UInt16 ComputeScalar(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt16 crc = InitialValue;

        // Process 8 bytes at once for larger inputs
        if (bytes.Length >= 8)
        {
            System.Int32 blockCount = bytes.Length / 8;
            System.Int32 remaining = bytes.Length % 8;

            // Process 8-byte blocks
            for (System.Int32 i = 0; i < blockCount * 8; i += 8)
            {
                crc = ProcessOctet(crc, bytes.Slice(i, 8));
            }

            // Process remaining bytes
            for (System.Int32 i = bytes.Length - remaining; i < bytes.Length; i++)
            {
                crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ bytes[i]) & 0xFF]);
            }
        }
        else
        {
            // For small inputs, use the simple loop
            for (System.Int32 i = 0; i < bytes.Length; i++)
            {
                crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ bytes[i]) & 0xFF]);
            }
        }

        return crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 ComputeSse42(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt16 crc = InitialValue;

        if (Sse42.IsSupported)
        {
            ref System.Byte start = ref MemoryMarshal.GetReference(bytes);
            System.Int32 length = bytes.Length;

            System.Int32 i = 0;
            for (; i + 8 <= length; i += 8)
            {
                crc = (System.UInt16)Sse42.Crc32(crc, Unsafe.ReadUnaligned<System.UInt32>(ref Unsafe.Add(ref start, i)));
            }

            for (; i < length; i++)
            {
                crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref start, i)) & 0xFF]);
            }
        }
        else
        {
            return ComputeScalar(bytes);
        }

        return crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 ComputeSimd(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt16 crc = InitialValue;
        System.Int32 vectorSize = Vector<System.Byte>.Count;
        System.Int32 vectorCount = bytes.Length / vectorSize;

        if (vectorCount > 0)
        {
            ref System.Byte start = ref MemoryMarshal.GetReference(bytes);
            System.Int32 i = 0;

            for (; i < vectorCount * vectorSize; i += vectorSize)
            {
                Vector<System.Byte> vec = Unsafe.ReadUnaligned<Vector<System.Byte>>(ref Unsafe.Add(ref start, i));
                crc = ProcessVector(crc, vec);
            }

            for (; i < bytes.Length; i++)
            {
                crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ Unsafe.Add(ref start, i)) & 0xFF]);
            }
        }
        else
        {
            return ComputeScalar(bytes);
        }

        return crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 ProcessVector(System.UInt16 crc, Vector<System.Byte> vec)
    {
        for (System.Int32 i = 0; i < Vector<System.Byte>.Count; i++)
        {
            crc = (System.UInt16)(crc >> 8 ^ Crc16LookupTable[(crc ^ vec[i]) & 0xFF]);
        }
        return crc;
    }

    #endregion Lookup Table Generation
}