using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace Nalix.Cryptography.Checksums;

/// <summary>
/// A high-performance CRC-32 implementation using reversed polynomial 0xEDB88320.
/// </summary>
public static class Crc32
{
    #region Constants

    private const System.UInt32 Polynomial = 0xEDB88320;
    private const System.UInt32 InitialValue = 0xFFFFFFFF;

    #endregion Constants

    #region Fields

    /// <summary>
    /// Precomputed lookup table for CRC-32 using polynomial 0xEDB88320.
    /// </summary>
    private static readonly System.UInt32[] Crc32LookupTable = Crc00.GenerateTable32(Polynomial);

    #endregion Fields

    #region APIs

    /// <summary>
    /// Computes the CRC-32 of the specified byte span.
    /// Uses hardware acceleration if available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Compute(System.ReadOnlySpan<System.Byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new System.ArgumentException("Byte span cannot be empty", nameof(bytes));
        }

        if (Sse42.IsSupported && bytes.Length >= 16)
        {
            return ComputeSse42(bytes);
        }

        return Vector.IsHardwareAccelerated && bytes.Length >= 32 ? ComputeSimd(bytes) : ComputeScalar(bytes);
    }

    /// <summary>
    /// Computes the CRC-32 of the specified byte array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Compute(params System.Byte[] bytes)
    {
        return bytes == null || bytes.Length == 0
            ? throw new System.ArgumentException("Byte array cannot be null or empty", nameof(bytes))
            : Compute(System.MemoryExtensions.AsSpan(bytes));
    }

    /// <summary>
    /// Computes the CRC-32 for a specified byte range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Compute(System.Byte[] bytes, System.Int32 start, System.Int32 length)
    {
        System.ArgumentNullException.ThrowIfNull(bytes);
        System.ArgumentOutOfRangeException.ThrowIfNegative(start);
        System.ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (bytes.Length == 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(bytes), "Byte array cannot be empty");
        }

        if (start >= bytes.Length && length > 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(start), "Start index is out of range");
        }

        System.Int32 end = start + length;
        return end > bytes.Length
            ? throw new System.ArgumentOutOfRangeException(nameof(length), "Specified length exceeds buffer bounds")
            : Compute(System.MemoryExtensions.AsSpan(bytes, start, length));
    }

    /// <summary>
    /// Computes the CRC-32 for any unmanaged type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 Compute<T>(System.ReadOnlySpan<T> data) where T : unmanaged
        => Compute(System.Runtime.InteropServices.MemoryMarshal.AsBytes(data));

    /// <summary>
    /// Verifies if the data matches the expected CRC-32 checksum.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Verify(System.ReadOnlySpan<System.Byte> data, System.UInt32 expectedCrc)
        => Compute(data) == expectedCrc;

    #endregion APIs

    #region Lookup Table Generation

    /// <summary>
    /// Processes 8 bytes at a time using lookup table.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// Scalar implementation of CRC-32 checksum.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// SIMD-accelerated CRC-32 computation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe System.UInt32 ComputeSimd(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt32 crc = InitialValue;
        System.Int32 vectorSize = Vector<System.Byte>.Count;
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
    /// SSE4.2 hardware-accelerated CRC-32 computation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe System.UInt32 ComputeSse42(System.ReadOnlySpan<System.Byte> bytes)
    {
        System.UInt32 crc = InitialValue;

        fixed (System.Byte* p = bytes)
        {
            System.Byte* ptr = p;
            System.Byte* end = p + bytes.Length;

            if (Sse42.X64.IsSupported)
            {
                while (ptr + 8 <= end)
                {
                    crc = (System.UInt32)Sse42.X64.Crc32(crc, *(System.UInt64*)ptr);
                    ptr += 8;
                }
            }

            while (ptr + 4 <= end)
            {
                crc = Sse42.Crc32(crc, *(System.UInt32*)ptr);
                ptr += 4;
            }

            while (ptr < end)
            {
                crc = Sse42.Crc32(crc, *ptr);
                ptr++;
            }
        }

        return ~crc;
    }

    #endregion Lookup Table Generation
}