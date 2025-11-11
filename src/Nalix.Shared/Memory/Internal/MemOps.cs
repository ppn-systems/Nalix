// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.LZ4.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.LZ4.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Internal;

/// <summary>
/// Provides low-level memory operations using unsafe code to perform optimized, high-performance memory manipulation.
/// </summary>
/// <remarks>
/// This class exposes a set of methods to perform various operations on memory, such as reading and writing unaligned data,
/// copying memory blocks, and comparing memory regions. It utilizes `unsafe` code to perform these operations directly
/// on raw memory, which allows for faster execution and is suitable for performance-critical applications like LZ compression/decompression.
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static unsafe class MemOps
{
    /// <summary>
    /// Reads an unaligned value from a memory location.
    /// </summary>
    /// <typeparam name="T">The type of the value to read. Must be unmanaged.</typeparam>
    /// <param name="source">A pointer to the source memory location.</param>
    /// <returns>The value read from the specified memory location.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static T ReadUnaligned<T>(System.Byte* source) where T : unmanaged
        => System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(source);

    /// <summary>
    /// Reads an unaligned value from a span of memory.
    /// </summary>
    /// <typeparam name="T">The type of the value to read. Must be unmanaged.</typeparam>
    /// <param name="source">A <see cref="System.ReadOnlySpan{Byte}"/> representing the source memory.</param>
    /// <returns>The value read from the specified memory location.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static T ReadUnaligned<T>(System.ReadOnlySpan<System.Byte> source) where T : unmanaged
    {
        fixed (System.Byte* pSource = &System.Runtime.InteropServices.MemoryMarshal.GetReference(source))
        {
            return System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(pSource);
        }
    }

    /// <summary>
    /// Writes an unaligned value to a memory location.
    /// </summary>
    /// <typeparam name="T">The type of the value to write. Must be unmanaged.</typeparam>
    /// <param name="destination">A pointer to the destination memory location.</param>
    /// <param name="value">The value to write.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void WriteUnaligned<T>(System.Byte* destination, T value) where T : unmanaged
        => System.Runtime.CompilerServices.Unsafe.WriteUnaligned(destination, value);

    /// <summary>
    /// Writes an unaligned value to a span of memory.
    /// </summary>
    /// <typeparam name="T">The type of the value to write. Must be unmanaged.</typeparam>
    /// <param name="destination">A <see cref="System.Span{Byte}"/> representing the destination memory.</param>
    /// <param name="value">The value to write.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void WriteUnaligned<T>(System.Span<System.Byte> destination, T value) where T : unmanaged
    {
        fixed (System.Byte* pDest = &System.Runtime.InteropServices.MemoryMarshal.GetReference(destination))
        {
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(pDest, value);
        }
    }

    /// <summary>
    /// Copies memory from source to destination, handling potential overlaps.
    /// </summary>
    /// <param name="source">A pointer to the source memory location.</param>
    /// <param name="destination">A pointer to the destination memory location.</param>
    /// <param name="length">The number of bytes to copy.</param>
    /// <remarks>
    /// This method ensures correct handling of memory overlaps, which is crucial when dealing with operations
    /// such as LZ decompression, where memory regions may overlap.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Copy(
        System.Byte* source,
        System.Byte* destination, System.Int32 length)
    {
        if (length <= 0 || source == destination)
        {
            return;
        }

        // Không chồng lấn:   [source .. source+length)  và  [destination .. destination+length) tách rời
        // Chồng lấn tiến:    destination > source && destination < source + length  => phải copy tiến từng byte
        // Chồng lấn lùi:     destination < source && source < destination + length  => có thể copy block an toàn (đọc trước ghi sau)

        if (destination < source || destination >= (source + length))
        {
            // Non-overlap or backward-overlap -> block copy OK (nhanh)
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(destination, source, (System.UInt32)length);
            return;
        }

        // Forward-overlap: copy từng byte theo chiều tiến để giữ semantics LZ backref
        for (System.Int32 i = 0; i < length; i++)
        {
            destination[i] = source[i];
        }
    }

    /// <summary>
    /// Copies memory from a source span to a destination pointer.
    /// </summary>
    /// <param name="source">A <see cref="System.ReadOnlySpan{Byte}"/> representing the source memory.</param>
    /// <param name="destination">A pointer to the destination memory location.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Copy(
        System.ReadOnlySpan<System.Byte> source,
        System.Byte* destination)
    {
        if (source.IsEmpty)
        {
            return;
        }

        fixed (System.Byte* pSource = &System.Runtime.InteropServices.MemoryMarshal.GetReference(source))
        {
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                destination, pSource, (System.UInt32)source.Length);
        }
    }

    /// <summary>
    /// Counts the number of matching bytes between two memory locations.
    /// </summary>
    /// <param name="p1">A pointer to the first memory region.</param>
    /// <param name="p2">A pointer to the second memory region.</param>
    /// <param name="maxLength">The maximum number of bytes to compare.</param>
    /// <returns>The number of matching bytes.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 CountEqualBytes(
        System.Byte* p1,
        System.Byte* p2,
        System.Int32 maxLength)
    {
        System.Int32 count = 0;
        if (maxLength <= 0)
        {
            return 0;
        }

        // -------------------- x86: AVX2 32B chunks --------------------
        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            while (count + 32 <= maxLength)
            {
                var a = System.Runtime.Intrinsics.X86.Avx.LoadVector256(p1 + count);
                var b = System.Runtime.Intrinsics.X86.Avx.LoadVector256(p2 + count);
                var cmp = System.Runtime.Intrinsics.X86.Avx2.CompareEqual(a, b);

                if (System.Runtime.Intrinsics.Vector256.EqualsAll(cmp, System.Runtime.Intrinsics.Vector256<System.Byte>.AllBitsSet))
                {
                    count += 32;
                    continue;
                }

                // Find first differing byte inside this 32B block
                System.Int32 mask = ~System.Runtime.Intrinsics.X86.Avx2.MoveMask(cmp); // 1 where bytes differ
                                                                                       // mask is 32-bit, each bit corresponds to a byte
                System.Int32 idx = System.Numerics.BitOperations.TrailingZeroCount(mask);
                return count + idx;
            }

            // Fall down to 16B SSE2 lane for the tail (if any)
            if (count + 16 <= maxLength)
            {
                var a = System.Runtime.Intrinsics.X86.Sse2.LoadVector128(p1 + count);
                var b = System.Runtime.Intrinsics.X86.Sse2.LoadVector128(p2 + count);
                var cmp = System.Runtime.Intrinsics.X86.Sse2.CompareEqual(a, b);

                if (System.Runtime.Intrinsics.Vector128.EqualsAll(cmp, System.Runtime.Intrinsics.Vector128<System.Byte>.AllBitsSet))
                {
                    count += 16;
                }
                else
                {
                    System.Int32 mask = ~System.Runtime.Intrinsics.X86.Sse2.MoveMask(cmp);
                    System.Int32 idx = System.Numerics.BitOperations.TrailingZeroCount(mask);
                    return count + idx;
                }
            }

            // 8-byte then scalar
            if (count + sizeof(System.UInt64) <= maxLength)
            {
                if (System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p1 + count) ==
                    System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p2 + count))
                {
                    count += sizeof(System.UInt64);
                }
                else
                {
                    // find first diff within 8 bytes
                    System.UInt64 x = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p1 + count);
                    System.UInt64 y = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p2 + count);
                    System.UInt64 d = x ^ y;
                    System.Int32 idx = System.Numerics.BitOperations.TrailingZeroCount(d) / 8;
                    return count + idx;
                }
            }

            while (count < maxLength && p1[count] == p2[count])
            {
                count++;
            }

            return count;
        }

        // -------------------- x86: SSE2 16B chunks --------------------
        if (System.Runtime.Intrinsics.X86.Sse2.IsSupported)
        {
            while (count + 16 <= maxLength)
            {
                var a = System.Runtime.Intrinsics.X86.Sse2.LoadVector128(p1 + count);
                var b = System.Runtime.Intrinsics.X86.Sse2.LoadVector128(p2 + count);
                var cmp = System.Runtime.Intrinsics.X86.Sse2.CompareEqual(a, b);

                if (System.Runtime.Intrinsics.Vector128.EqualsAll(cmp, System.Runtime.Intrinsics.Vector128<System.Byte>.AllBitsSet))
                {
                    count += 16;
                    continue;
                }

                System.Int32 mask = ~System.Runtime.Intrinsics.X86.Sse2.MoveMask(cmp);
                System.Int32 idx = System.Numerics.BitOperations.TrailingZeroCount(mask);
                return count + idx;
            }

            // 8-byte then scalar
            if (count + sizeof(System.UInt64) <= maxLength)
            {
                if (System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p1 + count) ==
                    System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p2 + count))
                {
                    count += sizeof(System.UInt64);
                }
                else
                {
                    System.UInt64 x = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p1 + count);
                    System.UInt64 y = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p2 + count);
                    System.UInt64 d = x ^ y;
                    System.Int32 idx = System.Numerics.BitOperations.TrailingZeroCount(d) / 8;
                    return count + idx;
                }
            }

            while (count < maxLength && p1[count] == p2[count])
            {
                count++;
            }

            return count;
        }

        // -------------------- ARM64: AdvSimd 16B chunks --------------------
        if (System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported)
        {
            while (count + 16 <= maxLength)
            {
                var a = System.Runtime.Intrinsics.Arm.AdvSimd.LoadVector128(p1 + count);
                var b = System.Runtime.Intrinsics.Arm.AdvSimd.LoadVector128(p2 + count);
                var cmp = System.Runtime.Intrinsics.Arm.AdvSimd.CompareEqual(a, b); // 0xFF where equal

                if (System.Runtime.Intrinsics.Vector128.EqualsAll(cmp, System.Runtime.Intrinsics.Vector128<System.Byte>.AllBitsSet))
                {
                    count += 16;
                    continue;
                }

                // Fallback: scan within this 16-byte chunk (cheap)
                for (System.Int32 j = 0; j < 16; j++)
                {
                    if (p1[count + j] != p2[count + j])
                    {
                        return count + j;
                    }
                }
                count += 16; // (shouldn't reach here)
            }

            // 8-byte then scalar
            if (count + sizeof(System.UInt64) <= maxLength)
            {
                if (System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p1 + count) ==
                    System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p2 + count))
                {
                    count += sizeof(System.UInt64);
                }
                else
                {
                    for (System.Int32 j = 0; j < 8; j++)
                    {
                        if (p1[count + j] != p2[count + j])
                        {
                            return count + j;
                        }
                    }
                    count += 8;
                }
            }

            while (count < maxLength && p1[count] == p2[count])
            {
                count++;
            }

            return count;
        }

        // -------------------- Portable fallback --------------------
        // (Already quite fast when paired with the 64-bit path above)
        while (count + sizeof(System.UInt64) <= maxLength &&
               System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p1 + count) ==
               System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p2 + count))
        {
            count += sizeof(System.UInt64);
        }
        while (count < maxLength && p1[count] == p2[count])
        {
            count++;
        }

        return count;
    }
}