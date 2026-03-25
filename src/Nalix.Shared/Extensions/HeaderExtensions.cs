// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides high‑performance helpers for reading packet header fields from serialized data.
/// </summary>
/// <remarks>
/// These APIs operate directly on <see cref="System.ReadOnlySpan{T}"/> to avoid allocations.
/// For protocol stability across platforms, use the explicit little‑endian readers.
/// </remarks>
public static class HeaderExtensions
{
    #region Little‑endian header readers (fixed offsets)

    /// <summary>
    /// Reads the 32‑bit <c>MagicNumber</c> at offset 0 in little‑endian format.
    /// </summary>
    /// <param name="this">The source buffer.</param>
    /// <returns>The magic number.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the buffer is too small.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint ReadMagicNumberLE(this System.ReadOnlySpan<byte> @this)
    {
        const int offs = (int)PacketHeaderOffset.MagicNumber;
        CheckSize(@this, offs, sizeof(uint));

        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(@this[..sizeof(uint)]);
    }

    /// <summary>
    /// Reads the 16‑bit <c>OpCode</c> at offset 4 in little‑endian format.
    /// </summary>
    /// <param name="this">The source buffer.</param>
    /// <returns>The operation code.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the buffer is too small.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ushort ReadOpCodeLE(this System.ReadOnlySpan<byte> @this)
    {
        const int offs = (int)PacketHeaderOffset.OpCode;
        CheckSize(@this, offs, sizeof(ushort));
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(@this[offs..]);
    }

    /// <summary>
    /// Reads the <see cref="PacketFlags"/> at offset 6.
    /// </summary>
    /// <param name="this">The source buffer.</param>
    /// <returns>The packet flags.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the buffer is too small.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static PacketFlags ReadFlagsLE(this System.ReadOnlySpan<byte> @this)
    {
        const int offs = (int)PacketHeaderOffset.Flags;
        CheckSize(@this, offs, sizeof(byte));
        return (PacketFlags)@this[offs];
    }

    /// <summary>
    /// Writes the <see cref="PacketFlags"/> value at offset 6.
    /// </summary>
    /// <param name="this">The destination buffer.</param>
    /// <param name="flags">The packet flags to write.</param>
    /// <exception cref="System.ArgumentException">Thrown when the buffer is too small.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void WriteFlagsLE(this System.Span<byte> @this, PacketFlags flags)
    {
        const int offs = (int)PacketHeaderOffset.Flags;
        CheckSize(@this, offs, sizeof(byte));
        @this[offs] = (byte)flags;
    }

    /// <summary>
    /// Reads the <see cref="PacketPriority"/> at offset 7.
    /// </summary>
    /// <param name="this">The source buffer.</param>
    /// <returns>The packet priority.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the buffer is too small.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static PacketPriority ReadPriorityLE(this System.ReadOnlySpan<byte> @this)
    {
        const int offs = (int)PacketHeaderOffset.Priority;
        CheckSize(@this, offs, sizeof(byte));
        return (PacketPriority)@this[offs];
    }

    /// <summary>
    /// Reads the <see cref="ProtocolType"/> at offset 8.
    /// </summary>
    /// <param name="this">The source buffer.</param>
    /// <returns>The transport protocol.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the buffer is too small.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ProtocolType ReadTransportLE(this System.ReadOnlySpan<byte> @this)
    {
        const int offs = (int)PacketHeaderOffset.Transport;
        CheckSize(@this, offs, sizeof(byte));
        return (ProtocolType)@this[offs];
    }

    /// <summary>
    /// Reads the 32‑bit <c>SequenceId</c> at its fixed offset in little‑endian format.
    /// </summary>
    /// <param name="this">The source buffer.</param>
    /// <returns>The sequence identifier.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the buffer is too small.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint ReadSequenceIdLE(this System.ReadOnlySpan<byte> @this)
    {
        const int offs = (int)PacketHeaderOffset.SequenceId;
        CheckSize(@this, offs, sizeof(uint));
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(@this.Slice(offs, sizeof(uint)));
    }

    #endregion Little‑endian header readers (fixed offsets)

    #region Helpers

    /// <summary>
    /// Throws when <paramref name="buffer"/> cannot supply <paramref name="size"/> bytes starting at <paramref name="offset"/>.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The starting offset.</param>
    /// <param name="size">The required byte count.</param>
    /// <exception cref="System.ArgumentException">Thrown when the buffer is too small.</exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void CheckSize(System.ReadOnlySpan<byte> buffer, int offset, int size)
    {
        if ((uint)offset > (uint)buffer.Length ||
            (uint)size > (uint)(buffer.Length - offset))
        {
            throw new System.ArgumentException($"Buffer is too small to read {size} bytes at offset {offset}.");
        }
    }

    #endregion Helpers
}
