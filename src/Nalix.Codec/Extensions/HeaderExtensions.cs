// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
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
    /// Gets a direct reference to the <see cref="PacketHeader"/> from the start of the buffer.
    /// Modifying the returned reference directly modifies the underlying span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketHeader AsHeaderRef(this Span<byte> @this)
    {
        if ((uint)@this.Length < PacketHeader.Size)
        {
            throw new ArgumentException(
                $"Buffer is too small for PacketHeader: length={@this.Length}, required={PacketHeader.Size}.");
        }

        return ref MemoryMarshal.AsRef<PacketHeader>(@this);
    }

    /// <summary>
    /// Gets a readonly reference to the <see cref="PacketHeader"/> from the start of the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly PacketHeader AsHeaderRef(this ReadOnlySpan<byte> @this)
    {
        if ((uint)@this.Length < PacketHeader.Size)
        {
            throw new ArgumentException(
                $"Buffer is too small for PacketHeader: length={@this.Length}, required={PacketHeader.Size}.");
        }

        return ref MemoryMarshal.AsRef<PacketHeader>(@this);
    }

    #endregion Header read/write (single struct)
}
