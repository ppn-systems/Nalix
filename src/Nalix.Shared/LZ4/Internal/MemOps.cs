using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Shared.LZ4.Internal;

/// <summary>
/// Provides low-level memory operations using unsafe code to perform optimized, high-performance memory manipulation.
/// </summary>
/// <remarks>
/// This class exposes a set of methods to perform various operations on memory, such as reading and writing unaligned data,
/// copying memory blocks, and comparing memory regions. It utilizes `unsafe` code to perform these operations directly
/// on raw memory, which allows for faster execution and is suitable for performance-critical applications like LZ compression/decompression.
/// </remarks>
public static unsafe class MemOps
{
    /// <summary>
    /// Reads an unaligned value from a memory location.
    /// </summary>
    /// <typeparam name="T">The type of the value to read. Must be unmanaged.</typeparam>
    /// <param name="source">A pointer to the source memory location.</param>
    /// <returns>The value read from the specified memory location.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadUnaligned<T>(byte* source) where T : unmanaged => Unsafe.ReadUnaligned<T>(source);

    /// <summary>
    /// Reads an unaligned value from a span of memory.
    /// </summary>
    /// <typeparam name="T">The type of the value to read. Must be unmanaged.</typeparam>
    /// <param name="source">A <see cref="System.ReadOnlySpan{Byte}"/> representing the source memory.</param>
    /// <returns>The value read from the specified memory location.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadUnaligned<T>(System.ReadOnlySpan<byte> source) where T : unmanaged
    {
        fixed (byte* pSource = &MemoryMarshal.GetReference(source))
            return Unsafe.ReadUnaligned<T>(pSource);
    }

    /// <summary>
    /// Writes an unaligned value to a memory location.
    /// </summary>
    /// <typeparam name="T">The type of the value to write. Must be unmanaged.</typeparam>
    /// <param name="destination">A pointer to the destination memory location.</param>
    /// <param name="value">The value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUnaligned<T>(byte* destination, T value) where T : unmanaged
        => Unsafe.WriteUnaligned(destination, value);

    /// <summary>
    /// Writes an unaligned value to a span of memory.
    /// </summary>
    /// <typeparam name="T">The type of the value to write. Must be unmanaged.</typeparam>
    /// <param name="destination">A <see cref="System.Span{Byte}"/> representing the destination memory.</param>
    /// <param name="value">The value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUnaligned<T>(System.Span<byte> destination, T value) where T : unmanaged
    {
        fixed (byte* pDest = &MemoryMarshal.GetReference(destination))
            Unsafe.WriteUnaligned(pDest, value);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(byte* source, byte* destination, int length)
    {
        // Unsafe.CopyBlockUnaligned handles overlaps correctly *if* src/dest don't overlap
        // in a way that would overwrite data needed later *in the same call*.
        // For LZ decompression (copying from already decompressed buffer), the typical
        // scenario is `destination > source` and `destination < source + length`.
        // A simple forward byte-by-byte copy works correctly here.
        // Higher performance methods exist but require care.
        if (length <= 0) return;

        // Use CopyBlockUnaligned for non-overlapping cases
        if (length > sizeof(ulong) &&
           (source < destination ||
            source >= destination + length))
        {
            Unsafe.CopyBlockUnaligned(destination, source, (uint)length);  // Fast non-overlapping copy
            return;
        }

        // For overlapping memory regions, fallback to a byte-by-byte copy
        for (int i = 0; i < length; ++i)
        {
            destination[i] = source[i];
        }
    }

    /// <summary>
    /// Copies memory from a source span to a destination pointer.
    /// </summary>
    /// <param name="source">A <see cref="System.ReadOnlySpan{Byte}"/> representing the source memory.</param>
    /// <param name="destination">A pointer to the destination memory location.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(System.ReadOnlySpan<byte> source, byte* destination)
    {
        if (source.IsEmpty) return;
        fixed (byte* pSource = &MemoryMarshal.GetReference(source))
        {
            Unsafe.CopyBlockUnaligned(destination, pSource, (uint)source.Length);
        }
    }

    /// <summary>
    /// Copies memory from a source pointer to a destination span.
    /// </summary>
    /// <param name="source">A pointer to the source memory location.</param>
    /// <param name="destination">A <see cref="System.Span{Byte}"/> representing the destination memory.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(byte* source, System.Span<byte> destination)
    {
        if (destination.IsEmpty) return;
        fixed (byte* pDest = &MemoryMarshal.GetReference(destination))
        {
            Unsafe.CopyBlockUnaligned(pDest, source, (uint)destination.Length);
        }
    }

    /// <summary>
    /// Compares two memory regions for equality.
    /// </summary>
    /// <param name="p1">A pointer to the first memory region.</param>
    /// <param name="p2">A pointer to the second memory region.</param>
    /// <param name="length">The number of bytes to compare.</param>
    /// <returns><c>true</c> if the two memory regions are equal, otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SequenceEqual(byte* p1, byte* p2, int length)
    {
        if (length <= 0) return true;
        // Fallback for very small lengths where ReadUnaligned might be slower setup
        if (length < sizeof(ulong))
        {
            for (int i = 0; i < length; ++i)
            {
                if (p1[i] != p2[i]) return false;
            }
            return true;
        }

        int n = 0;
        while (length >= sizeof(ulong))
        {
            if (Unsafe.ReadUnaligned<ulong>(p1 + n) != Unsafe.ReadUnaligned<ulong>(p2 + n))
                return false;
            n += sizeof(ulong);
            length -= sizeof(ulong);
        }
        // Check remaining bytes (up to 7)
        if (length >= sizeof(uint))
        {
            if (Unsafe.ReadUnaligned<uint>(p1 + n) != Unsafe.ReadUnaligned<uint>(p2 + n))
                return false;
            n += sizeof(uint);
            length -= sizeof(uint);
        }
        if (length >= sizeof(ushort))
        {
            if (Unsafe.ReadUnaligned<ushort>(p1 + n) != Unsafe.ReadUnaligned<ushort>(p2 + n))
                return false;
            n += sizeof(ushort);
            length -= sizeof(ushort);
        }
        if (length > 0) // Remaining byte
        {
            if (Unsafe.ReadUnaligned<byte>(p1 + n) != Unsafe.ReadUnaligned<byte>(p2 + n))
                return false;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountEqualBytes(byte* p1, byte* p2, int maxLength)
    {
        int count = 0;

        // Optimize for common case using 64-bit reads
        while (count + sizeof(ulong) <= maxLength &&
            Unsafe.ReadUnaligned<ulong>(p1 + count) == Unsafe.ReadUnaligned<ulong>(p2 + count))
            count += sizeof(ulong);

        // Check remaining bytes (up to 7)
        while (count < maxLength && p1[count] == p2[count]) count++;

        return count;
    }
}
