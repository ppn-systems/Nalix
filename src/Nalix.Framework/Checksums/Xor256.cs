// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Checksums;

/// <summary>
/// Provides a highly optimized XOR checksum implementation using unsafe memory operations.
/// </summary>
public static class Xor256
{
    /// <summary>
    /// Computes the XOR checksum over a byte span.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute(System.ReadOnlySpan<System.Byte> data)
    {
        if (data.IsEmpty)
        {
            throw new System.ArgumentException("Data cannot be empty", nameof(data));
        }

        System.Byte xor = 0;

        unsafe
        {
            ref System.Byte src = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(data);
            System.Int32 length = data.Length;

            System.Int32 i = 0;

            // Process 8 bytes at a time
            if (length >= sizeof(System.UInt64))
            {
                fixed (System.Byte* p = &src)
                {
                    System.UInt64* ptr = (System.UInt64*)p;
                    System.Int32 ulongCount = length / sizeof(System.UInt64);

                    System.UInt64 accum = 0;
                    for (System.Int32 j = 0; j < ulongCount; j++)
                    {
                        accum ^= ptr[j];
                    }

                    // Fold ulong into byte
                    xor ^= (System.Byte)(accum & 0xFF);
                    xor ^= (System.Byte)(accum >> 8 & 0xFF);
                    xor ^= (System.Byte)(accum >> 16 & 0xFF);
                    xor ^= (System.Byte)(accum >> 24 & 0xFF);
                    xor ^= (System.Byte)(accum >> 32 & 0xFF);
                    xor ^= (System.Byte)(accum >> 40 & 0xFF);
                    xor ^= (System.Byte)(accum >> 48 & 0xFF);
                    xor ^= (System.Byte)(accum >> 56 & 0xFF);

                    i = ulongCount * sizeof(System.UInt64);
                }
            }

            // Process remaining bytes
            for (; i < length; i++)
            {
                xor ^= System.Runtime.CompilerServices.Unsafe.Add(ref src, i);
            }
        }

        return xor;
    }

    /// <summary>
    /// Computes the XOR checksum from a byte array.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute(params System.Byte[] data)
    {
        System.ArgumentNullException.ThrowIfNull(data);
        return Compute(System.MemoryExtensions.AsSpan(data));
    }

    /// <summary>
    /// Computes XOR checksum over any unmanaged data.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute<T>(System.ReadOnlySpan<T> data) where T : unmanaged
        => Compute(System.Runtime.InteropServices.MemoryMarshal.AsBytes(data));

    /// <summary>
    /// Verifies that the computed XOR matches the expected checksum.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Verify(System.ReadOnlySpan<System.Byte> data, System.Byte expectedXor)
        => Compute(data) == expectedXor;
}