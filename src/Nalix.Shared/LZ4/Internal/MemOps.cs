using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Shared.LZ4.Internal;

/// <summary>
/// Low-level memory operations using unsafe code.
/// </summary>
internal static unsafe class MemOps
{
    /// <summary>
    /// Reads an unaligned value from a memory location.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadUnaligned<T>(byte* source) where T : unmanaged => Unsafe.ReadUnaligned<T>(source);

    /// <summary>
    /// Reads an unaligned value from a span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadUnaligned<T>(ReadOnlySpan<byte> source) where T : unmanaged
    {
        fixed (byte* pSource = &MemoryMarshal.GetReference(source))
        {
            return Unsafe.ReadUnaligned<T>(pSource);
        }
    }

    /// <summary>
    /// Writes an unaligned value to a memory location.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUnaligned<T>(byte* destination, T value) where T : unmanaged => Unsafe.WriteUnaligned(destination, value);

    /// <summary>
    /// Writes an unaligned value to a span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUnaligned<T>(Span<byte> destination, T value) where T : unmanaged
    {
        fixed (byte* pDest = &MemoryMarshal.GetReference(destination))
        {
            Unsafe.WriteUnaligned(pDest, value);
        }
    }

    /// <summary>
    /// Copies memory from source to destination. Handles potential overlaps suitable for LZ decompression.
    /// </summary>
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

        // Simple, safe byte-by-byte copy handles required LZ overlap
        for (int i = 0; i < length; ++i)
        {
            destination[i] = source[i];
        }
        // Alternatively, for non-overlapping or simple cases, use CopyBlock:
        // Unsafe.CopyBlockUnaligned(destination, source, (uint)length);
    }

    /// <summary>
    /// Copies memory from source span to destination pointer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(ReadOnlySpan<byte> source, byte* destination)
    {
        if (source.IsEmpty) return;
        fixed (byte* pSource = &MemoryMarshal.GetReference(source))
        {
            Unsafe.CopyBlockUnaligned(destination, pSource, (uint)source.Length);
        }
    }

    /// <summary>
    /// Copies memory from source pointer to destination span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(byte* source, Span<byte> destination)
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
