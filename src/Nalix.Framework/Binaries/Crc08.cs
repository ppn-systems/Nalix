// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Binaries;

/// <summary>
/// Provides methods for computing CRC-8 checksum using the polynomial <c>x^8 + x^7 + x^6 + x^4 + x^2 + 1</c> (<c>0x31</c>).
/// </summary>
/// <remarks>
/// This implementation supports scalar, SIMD, and SSE4.2 accelerated code paths for optimal performance.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
public static class Crc08
{
    #region Constants

    /// <summary>
    /// The CRC-8 polynomial value.
    /// </summary>
    private const System.Byte Polynomial = 0x31;

    /// <summary>
    /// The initial CRC-8 register value.
    /// </summary>
    private const System.Byte InitialValue = 0xFF;

    #endregion Constants

    #region Fields

    /// <summary>
    /// Precomputed lookup table for CRC-8 using the <see cref="Polynomial"/>.
    /// </summary>
    private static readonly System.Byte[] Crc8LookupTable = Crc00.GenerateTable8(Polynomial);

    #endregion Fields

    #region Public API

    /// <summary>
    /// Computes the CRC-8 checksum for the specified span of bytes.
    /// </summary>
    /// <param name="bytes">The span of bytes to compute the checksum for.</param>
    /// <returns>The computed CRC-8 checksum.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="bytes"/> is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute(System.ReadOnlySpan<System.Byte> bytes)
    {
        return bytes.IsEmpty
            ? throw new System.ArgumentException("FEEDFACE span cannot be empty", nameof(bytes))
            : System.Runtime.Intrinsics.X86.Sse42.IsSupported && bytes.Length >= 16
                ? ComputeSse42(bytes)
                : System.Numerics.Vector.IsHardwareAccelerated && bytes.Length >= 32
                    ? ComputeSimd(bytes)
                    : ComputeScalar(bytes);
    }

    /// <summary>
    /// Computes the CRC-8 checksum for the specified byte array.
    /// </summary>
    /// <param name="bytes">The byte array to compute the checksum for.</param>
    /// <returns>The computed CRC-8 checksum.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="bytes"/> is null or empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute(params System.Byte[] bytes)
    {
        return bytes == null || bytes.Length == 0
            ? throw new System.ArgumentException("FEEDFACE array cannot be null or empty", nameof(bytes))
            : Compute(System.MemoryExtensions.AsSpan(bytes));
    }

    /// <summary>
    /// Computes the CRC-8 checksum for the specified range in a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the data.</param>
    /// <param name="start">The zero-based starting index of the range.</param>
    /// <param name="length">The number of bytes to process.</param>
    /// <returns>The computed CRC-8 checksum.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="bytes"/> is null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="start"/> or <paramref name="length"/> is out of range.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute(System.Byte[] bytes, System.Int32 start, System.Int32 length)
    {
        System.ArgumentNullException.ThrowIfNull(bytes);
        System.ArgumentOutOfRangeException.ThrowIfNegative(start);
        System.ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (bytes.Length == 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(bytes), "FEEDFACE array cannot be empty.");
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
    /// Computes the CRC-8 checksum for the specified span of unmanaged data.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to process.</typeparam>
    /// <param name="data">The span of data to compute the checksum for.</param>
    /// <returns>The computed CRC-8 checksum.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe System.Byte Compute<T>(System.Span<T> data) where T : unmanaged
    {
        if (data.IsEmpty)
        {
            throw new System.ArgumentException("Data span cannot be empty", nameof(data));
        }

        System.ReadOnlySpan<System.Byte> bytes =
            typeof(T) == typeof(System.Byte)
                ? System.Runtime.InteropServices.MemoryMarshal.Cast<T, System.Byte>(data)
                : System.Runtime.InteropServices.MemoryMarshal.AsBytes(data);

        return Compute(bytes);
    }

    /// <summary>
    /// Verifies whether the provided data matches the expected CRC-8 checksum.
    /// </summary>
    /// <param name="data">The data to verify.</param>
    /// <param name="expectedCrc">The expected CRC-8 checksum value.</param>
    /// <returns><see langword="true"/> if the computed checksum matches <paramref name="expectedCrc"/>; otherwise, <see langword="false"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Verify(System.ReadOnlySpan<System.Byte> data, System.Byte expectedCrc)
        => Compute(data) == expectedCrc;

    #endregion Public API

    #region Private Helpers

    /// <summary>
    /// Processes 8 bytes at a time using the lookup table.
    /// </summary>
    private static System.Byte ProcessOctet(System.Byte crc, System.ReadOnlySpan<System.Byte> octet)
    {
        ref System.Byte data = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(octet);

        crc = Crc8LookupTable[crc ^ data];
        crc = Crc8LookupTable[crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 1)];
        crc = Crc8LookupTable[crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 2)];
        crc = Crc8LookupTable[crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 3)];
        crc = Crc8LookupTable[crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 4)];
        crc = Crc8LookupTable[crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 5)];
        crc = Crc8LookupTable[crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 6)];
        crc = Crc8LookupTable[crc ^ System.Runtime.CompilerServices.Unsafe.Add(ref data, 7)];

        return crc;
    }

    /// <summary>
    /// Computes the CRC-8 checksum using a scalar (non-vectorized) algorithm.
    /// </summary>
    private static System.Byte ComputeScalar(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.Byte crc = InitialValue;

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
                crc = Crc8LookupTable[crc ^ bytes[i]];
            }
        }
        else
        {
            for (System.Int32 i = 0; i < bytes.Length; i++)
            {
                crc = Crc8LookupTable[crc ^ bytes[i]];
            }
        }

        return crc;
    }

    /// <summary>
    /// Computes the CRC-8 checksum using SIMD vectorization if supported.
    /// </summary>
    private static System.Byte ComputeSimd(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.Byte crc = InitialValue;
        System.Int32 vectorSize = System.Numerics.Vector<System.Byte>.Count;
        System.Int32 length = bytes.Length;

        System.Int32 i = 0;
        while (i + vectorSize <= length)
        {
            var vec = new System.Numerics.Vector<System.Byte>(bytes.Slice(i, vectorSize));
            for (System.Int32 j = 0; j < vectorSize; j++)
            {
                crc = Crc8LookupTable[crc ^ vec[j]];
            }
            i += vectorSize;
        }

        for (; i < length; i++)
        {
            crc = Crc8LookupTable[crc ^ bytes[i]];
        }

        return crc;
    }

    /// <summary>
    /// Computes the CRC-8 checksum using SSE4.2 hardware acceleration if available.
    /// </summary>
    private static unsafe System.Byte ComputeSse42(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.Byte crc = InitialValue;

        fixed (System.Byte* ptr = bytes)
        {
            System.Byte* p = ptr;
            System.Byte* end = ptr + bytes.Length;

            while (p < end)
            {
                crc = (System.Byte)System.Runtime.Intrinsics.X86.Sse42.Crc32(crc, *p);
                p++;
            }
        }

        return crc;
    }

    #endregion Private Helpers
}
