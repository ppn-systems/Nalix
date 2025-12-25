// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Internal;

/// <summary>
/// Provides methods to efficiently write literal bytes directly to a memory destination.
/// This class is optimized for high-performance memory operations with minimal overhead.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(
        ref byte* destPtr,
        byte* literalStartPtr,
        int length)
    {
        if ((uint)length == 0u)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(
        ref byte* destPtr,
        ReadOnlySpan<byte> literals)
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
