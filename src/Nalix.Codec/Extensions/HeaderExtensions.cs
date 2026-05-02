// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Abstractions.Primitives;

namespace Nalix.Codec.Extensions;

/// <summary>
/// Provides high-performance helpers for reading and writing the <see cref="PacketHeader"/>
/// from serialized data as a single 10-byte struct read/write.
/// </summary>
public static class HeaderExtensions
{
    #region Header read/write (single struct)

    /// <summary>
    /// Reads the full <see cref="PacketHeader"/> (10 bytes) from the start of the buffer
    /// in one shot using <see cref="MemoryMarshal.Read{T}"/>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PacketHeader ReadHeaderLE(this ReadOnlySpan<byte> @this)
    {
        if ((uint)@this.Length < PacketHeader.Size)
        {
            throw new ArgumentException(
                $"Buffer is too small to read PacketHeader: length={@this.Length}, required={PacketHeader.Size}.");
        }

        return MemoryMarshal.Read<PacketHeader>(@this);
    }

    /// <summary>
    /// Writes the full <see cref="PacketHeader"/> (10 bytes) to the start of the buffer
    /// in one shot using <see cref="MemoryMarshal.Write{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteHeaderLE(this Span<byte> @this, PacketHeader header)
    {
        if ((uint)@this.Length < PacketHeader.Size)
        {
            throw new ArgumentException(
                $"Buffer is too small to write PacketHeader: length={@this.Length}, required={PacketHeader.Size}.");
        }

        MemoryMarshal.Write(@this, in header);
    }

    #endregion Header read/write (single struct)
}
