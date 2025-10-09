// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Binaries;

/// <summary>
/// Provides a highly optimized XOR checksum implementation using unsafe memory operations.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
public static class Xor256
{
    /// <summary>
    /// Computes the XOR checksum over a read-only span of bytes.
    /// </summary>
    /// <param name="data">The read-only span of bytes to compute the XOR checksum for.</param>
    /// <returns>The computed XOR checksum as an 8-bit unsigned integer.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    /// <remarks>
    /// This method uses unsafe memory operations to process 8 bytes at a time for performance optimization.
    /// Remaining bytes are processed individually.
    /// </remarks>
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
    /// Computes the XOR checksum over a byte array.
    /// </summary>
    /// <param name="data">The byte array to compute the XOR checksum for.</param>
    /// <returns>The computed XOR checksum as an 8-bit unsigned integer.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute(params System.Byte[] data)
    {
        System.ArgumentNullException.ThrowIfNull(data);
        return Compute(System.MemoryExtensions.AsSpan(data));
    }

    /// <summary>
    /// Computes the XOR checksum over a read-only span of unmanaged data.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the data.</typeparam>
    /// <param name="data">The read-only span of unmanaged data to compute the XOR checksum for.</param>
    /// <returns>The computed XOR checksum as an 8-bit unsigned integer.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Byte Compute<T>(System.ReadOnlySpan<T> data) where T : unmanaged
        => Compute(System.Runtime.InteropServices.MemoryMarshal.AsBytes(data));

    /// <summary>
    /// Verifies whether the computed XOR checksum matches the expected checksum.
    /// </summary>
    /// <param name="data">The read-only span of bytes to verify.</param>
    /// <param name="expectedXor">The expected XOR checksum to compare against.</param>
    /// <returns><c>true</c> if the computed XOR checksum matches <paramref name="expectedXor"/>; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Verify(System.ReadOnlySpan<System.Byte> data, System.Byte expectedXor) => Compute(data) == expectedXor;
}