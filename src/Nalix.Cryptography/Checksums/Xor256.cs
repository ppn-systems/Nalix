using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Cryptography.Checksums;

/// <summary>
/// Provides a highly optimized XOR checksum implementation using unsafe memory operations.
/// </summary>
public static class Xor256
{
    /// <summary>
    /// Computes the XOR checksum over a byte span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Compute(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty", nameof(data));

        byte xor = 0;

        unsafe
        {
            ref byte src = ref MemoryMarshal.GetReference(data);
            int length = data.Length;

            int i = 0;

            // Process 8 bytes at a time
            if (length >= sizeof(ulong))
            {
                fixed (byte* p = &src)
                {
                    ulong* ptr = (ulong*)p;
                    int ulongCount = length / sizeof(ulong);

                    ulong accum = 0;
                    for (int j = 0; j < ulongCount; j++)
                        accum ^= ptr[j];

                    // Fold ulong into byte
                    xor ^= (byte)(accum & 0xFF);
                    xor ^= (byte)((accum >> 8) & 0xFF);
                    xor ^= (byte)((accum >> 16) & 0xFF);
                    xor ^= (byte)((accum >> 24) & 0xFF);
                    xor ^= (byte)((accum >> 32) & 0xFF);
                    xor ^= (byte)((accum >> 40) & 0xFF);
                    xor ^= (byte)((accum >> 48) & 0xFF);
                    xor ^= (byte)((accum >> 56) & 0xFF);

                    i = ulongCount * sizeof(ulong);
                }
            }

            // Process remaining bytes
            for (; i < length; i++)
                xor ^= Unsafe.Add(ref src, i);
        }

        return xor;
    }

    /// <summary>
    /// Computes the XOR checksum from a byte array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Compute(params byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Compute(data.AsSpan());
    }

    /// <summary>
    /// Computes XOR checksum over any unmanaged data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Compute<T>(ReadOnlySpan<T> data) where T : unmanaged
        => Compute(MemoryMarshal.AsBytes(data));

    /// <summary>
    /// Verifies that the computed XOR matches the expected checksum.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Verify(ReadOnlySpan<byte> data, byte expectedXor)
        => Compute(data) == expectedXor;
}