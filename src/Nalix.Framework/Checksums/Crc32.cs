// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Checksums;

/// <summary>
/// Provides methods for computing the CRC-32 checksum using the reversed polynomial <c>0xEDB88320</c>.
/// </summary>
/// <remarks>
/// This implementation supports hardware acceleration using SSE4.2 and SIMD instructions
/// for optimal performance on supported platforms.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
public static class Crc32
{
    #region Constants

    /// <summary>
    /// The reversed CRC-32 polynomial value.
    /// </summary>
    private const System.UInt32 Polynomial = 0xEDB88320;

    /// <summary>
    /// The initial CRC-32 register value.
    /// </summary>
    private const System.UInt32 InitialValue = 0xFFFFFFFF;

    #endregion Constants

    #region Fields

    /// <summary>
    /// Precomputed lookup table for CRC-32 checksum using the <see cref="Polynomial"/>.
    /// </summary>
    private static readonly System.UInt32[] Crc32LookupTable = Crc00.GenerateTable32(Polynomial);

    #endregion Fields

    #region Public API

    /// <summary>
    /// Computes the CRC-32 checksum for the specified read-only span of bytes.
    /// </summary>
    /// <param name="bytes">The span of bytes to compute the checksum for.</param>
    /// <returns>The computed CRC-32 checksum.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="bytes"/> is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Compute(System.ReadOnlySpan<System.Byte> bytes)
    {
        return bytes.IsEmpty
            ? throw new System.ArgumentException("Byte span cannot be empty", nameof(bytes))
            : System.Runtime.Intrinsics.X86.Sse42.IsSupported && bytes.Length >= 16
            ? ComputeSse42(bytes)
            : System.Numerics.Vector.IsHardwareAccelerated && bytes.Length >= 32
            ? ComputeSimd(bytes)
            : ComputeScalar(bytes);
    }

    /// <summary>
    /// Computes the CRC-32 checksum for the specified byte array.
    /// </summary>
    /// <param name="bytes">The byte array to compute the checksum for.</param>
    /// <returns>The computed CRC-32 checksum.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="bytes"/> is null or empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Compute(params System.Byte[] bytes)
    {
        return bytes == null || bytes.Length == 0
            ? throw new System.ArgumentException("Byte array cannot be null or empty", nameof(bytes))
            : Compute(System.MemoryExtensions.AsSpan(bytes));
    }

    /// <summary>
    /// Computes the CRC-32 checksum for the specified range within a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the data.</param>
    /// <param name="start">The zero-based starting index of the range.</param>
    /// <param name="length">The number of bytes to include in the computation.</param>
    /// <returns>The computed CRC-32 checksum.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="bytes"/> is null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="start"/> or <paramref name="length"/> is out of range.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Compute(System.Byte[] bytes, System.Int32 start, System.Int32 length)
    {
        System.ArgumentNullException.ThrowIfNull(bytes);
        System.ArgumentOutOfRangeException.ThrowIfNegative(start);
        System.ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (bytes.Length == 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(bytes), "Byte array cannot be empty.");
        }

        if (start >= bytes.Length && length > 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(start), "Start index is out of range.");
        }

        System.Int32 end = start + length;
        return end > bytes.Length
            ? throw new System.ArgumentOutOfRangeException(nameof(length), "Specified length exceeds buffer bounds.")
            : Compute(System.MemoryExtensions.AsSpan(bytes, start, length));
    }

    /// <summary>
    /// Computes the CRC-32 checksum for any unmanaged type.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to compute the checksum for.</typeparam>
    /// <param name="data">The data to compute the checksum for.</param>
    /// <returns>The computed CRC-32 checksum.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Compute<T>(System.ReadOnlySpan<T> data) where T : unmanaged
        => Compute(System.Runtime.InteropServices.MemoryMarshal.AsBytes(data));

    /// <summary>
    /// Verifies whether the provided data matches the expected CRC-32 checksum.
    /// </summary>
    /// <param name="data">The data to verify.</param>
    /// <param name="expectedCrc">The expected CRC-32 checksum value.</param>
    /// <returns><see langword="true"/> if the computed checksum matches <paramref name="expectedCrc"/>; otherwise, <see langword="false"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Verify(System.ReadOnlySpan<System.Byte> data, System.UInt32 expectedCrc)
        => Compute(data) == expectedCrc;

    #endregion Public API

    #region Private Helpers

    /// <summary>
    /// Processes 8 bytes at a time using the lookup table.
    /// </summary>
    private static System.UInt32 ProcessOctet(System.UInt32 crc, System.ReadOnlySpan<System.Byte> octet)
    {
        ref System.Byte data = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(octet);

        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ data];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 1)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 2)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 3)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 4)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 5)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 6)];
        crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 7)];

        return crc;
    }

    /// <summary>
    /// Computes the CRC-32 checksum using a scalar (non-vectorized) algorithm.
    /// </summary>
    private static System.UInt32 ComputeScalar(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt32 crc = InitialValue;

        if (bytes.Length >= 8)
        {
            System.Int32 unaligned = bytes.Length % 8;
            System.Int32 aligned = bytes.Length - unaligned;

            for (System.Int32 i = 0; i < aligned; i += 8)
            {
                crc = ProcessOctet(crc, bytes.Slice(i, 8));
            }

            for (System.Int32 i = aligned; i < bytes.Length; i++)
            {
                crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ bytes[i]];
            }
        }
        else
        {
            for (System.Int32 i = 0; i < bytes.Length; i++)
            {
                crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ bytes[i]];
            }
        }

        return ~crc;
    }

    /// <summary>
    /// Computes the CRC-32 checksum using SIMD vectorization if supported.
    /// </summary>
    private static unsafe System.UInt32 ComputeSimd(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt32 crc = InitialValue;
        System.Int32 vectorSize = System.Numerics.Vector<System.Byte>.Count;
        System.Int32 length = bytes.Length;

        fixed (System.Byte* ptr = bytes)
        {
            System.Int32 i = 0;
            while (i + vectorSize <= length)
            {
                for (System.Int32 j = 0; j < vectorSize; j++)
                {
                    crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ ptr[i + j]];
                }

                i += vectorSize;
            }

            for (; i < length; i++)
            {
                crc = (crc >> 8) ^ Crc32LookupTable[(crc & 0xFF) ^ ptr[i]];
            }
        }

        return ~crc;
    }

    /// <summary>
    /// Computes the CRC-32 checksum using SSE4.2 hardware acceleration if available.
    /// </summary>
    private static unsafe System.UInt32 ComputeSse42(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt32 crc = InitialValue;

        fixed (System.Byte* p = bytes)
        {
            System.Byte* ptr = p;
            System.Byte* end = p + bytes.Length;

            if (System.Runtime.Intrinsics.X86.Sse42.X64.IsSupported)
            {
                while (ptr + 8 <= end)
                {
                    crc = (System.UInt32)System.Runtime.Intrinsics.X86.Sse42.X64.Crc32(crc, *(System.UInt64*)ptr);
                    ptr += 8;
                }
            }

            while (ptr + 4 <= end)
            {
                crc = System.Runtime.Intrinsics.X86.Sse42.Crc32(crc, *(System.UInt32*)ptr);
                ptr += 4;
            }

            while (ptr < end)
            {
                crc = System.Runtime.Intrinsics.X86.Sse42.Crc32(crc, *ptr);
                ptr++;
            }
        }

        return ~crc;
    }

    #endregion Private Helpers
}
