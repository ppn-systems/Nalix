// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Memory.Unsafe;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.LZ4.Internal;

/// <summary>
/// Provides methods to efficiently write literal bytes directly to a memory destination.
/// This class is optimized for high-performance memory operations with minimal overhead.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static unsafe class LiteralWriter
{
    /// <summary>
    /// Writes a sequence of literal bytes from a memory pointer to the destination.
    /// </summary>
    /// <param name="destPtr">A reference to the destination memory pointer. This pointer will be updated after writing.</param>
    /// <param name="literalStartPtr">The pointer to the start of the literal data to be written.</param>
    /// <param name="length">The number of bytes to write from the literal data. Must be non-negative.</param>
    /// <remarks>
    /// This method directly copies bytes from one memory location to another.
    /// It is assumed that `destPtr` has enough space to accommodate `length` bytes.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(
        ref System.Byte* destPtr,
        System.Byte* literalStartPtr,
        System.Int32 length)
    {
        if ((System.UInt32)length == 0u)
        {
            return;
        }

        // Copy the data from source pointer to destination pointer
        MemOps.Copy(literalStartPtr, destPtr, length);

        // Advance the destination pointer by the number of bytes written
        destPtr += length;
    }

    /// <summary>
    /// Writes a sequence of literal bytes from a read-only span to the destination.
    /// </summary>
    /// <param name="destPtr">A reference to the destination memory pointer. This pointer will be updated after writing.</param>
    /// <param name="literals">The span of bytes containing the literal data to be written.</param>
    /// <remarks>
    /// This method is designed to handle scenarios where the source data is in a span.
    /// Spans provide safer and more flexible memory access compared to raw pointers.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(
        ref System.Byte* destPtr,
        System.ReadOnlySpan<System.Byte> literals)
    {
        if (literals.Length == 0)
        {
            return;
        }

        // Copy the data from the span to the destination pointer
        MemOps.Copy(literals, destPtr);

        // Advance the destination pointer by the number of bytes written
        destPtr += literals.Length;
    }
}
