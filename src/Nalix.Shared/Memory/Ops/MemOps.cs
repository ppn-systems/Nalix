// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Unsafe;

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
public static unsafe class MemOps
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
        // Unsafe.CopyBlockUnaligned handles overlaps correctly *if* src/dest don't overlap
        // in a way that would overwrite data needed later *in the same call*.
        // For LZ decompression (copying from already decompressed buffer), the typical
        // scenario is `destination > source` and `destination < source + length`.
        // A simple forward byte-by-byte copy works correctly here.
        // Higher performance methods exist but require care.
        if (length <= 0)
        {
            return;
        }

        // Simple, safe byte-by-byte copy handles required LZ overlap
        for (System.Int32 i = 0; i < length; ++i)
        {
            destination[i] = source[i];
        }
        // Alternatively, for non-overlapping or simple cases, use CopyBlock:
        // Unsafe.CopyBlockUnaligned(destination, source, (uint)length);
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
    /// Copies memory from a source pointer to a destination span.
    /// </summary>
    /// <param name="source">A pointer to the source memory location.</param>
    /// <param name="destination">A <see cref="System.Span{Byte}"/> representing the destination memory.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Copy(
        System.Byte* source,
        System.Span<System.Byte> destination)
    {
        if (destination.IsEmpty)
        {
            return;
        }

        fixed (System.Byte* pDest = &System.Runtime.InteropServices.MemoryMarshal.GetReference(destination))
        {
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(
                pDest, source, (System.UInt32)destination.Length);
        }
    }

    /// <summary>
    /// Compares two memory regions for equality.
    /// </summary>
    /// <param name="p1">A pointer to the first memory region.</param>
    /// <param name="p2">A pointer to the second memory region.</param>
    /// <param name="length">The number of bytes to compare.</param>
    /// <returns><c>true</c> if the two memory regions are equal, otherwise <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean SequenceEqual(
        System.Byte* p1,
        System.Byte* p2,
        System.Int32 length)
    {
        if (length <= 0)
        {
            return true;
        }
        // Fallback for very small lengths where ReadUnaligned might be slower setup
        if (length < sizeof(System.UInt64))
        {
            for (System.Int32 i = 0; i < length; ++i)
            {
                if (p1[i] != p2[i])
                {
                    return false;
                }
            }
            return true;
        }

        System.Int32 n = 0;
        while (length >= sizeof(System.UInt64))
        {
            if (System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p1 + n) !=
                System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p2 + n))
            {
                return false;
            }

            n += sizeof(System.UInt64);
            length -= sizeof(System.UInt64);
        }
        // Check remaining bytes (up to 7)
        if (length >= sizeof(System.UInt32))
        {
            if (System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(p1 + n) !=
                System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(p2 + n))
            {
                return false;
            }

            n += sizeof(System.UInt32);
            length -= sizeof(System.UInt32);
        }
        if (length >= sizeof(System.UInt16))
        {
            if (System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt16>(p1 + n) !=
                System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt16>(p2 + n))
            {
                return false;
            }

            n += sizeof(System.UInt16);
            length -= sizeof(System.UInt16);
        }
        if (length > 0) // Remaining byte
        {
            if (System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Byte>(p1 + n) !=
                System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Byte>(p2 + n))
            {
                return false;
            }
        }

        return true;
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

        // Optimize for common case using 64-bit reads
        while (count + sizeof(System.UInt64) <= maxLength &&
            System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p1 + count) ==
            System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt64>(p2 + count))
        {
            count += sizeof(System.UInt64);
        }

        // Check remaining bytes (up to 7)
        while (count < maxLength && p1[count] == p2[count])
        {
            count++;
        }

        return count;
    }
}