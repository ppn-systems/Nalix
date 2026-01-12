// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Protocols;

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
    public static System.UInt32 ReadMagicNumberLE(this System.ReadOnlySpan<System.Byte> @this)
    {
        const System.Int32 offs = (System.Int32)PacketHeaderOffset.MAGIC_NUMBER;
        CheckSize(@this, offs, sizeof(System.UInt32));

        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(@this[..sizeof(System.UInt32)]);
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
    public static System.UInt16 ReadOpCodeLE(this System.ReadOnlySpan<System.Byte> @this)
    {
        const System.Int32 offs = (System.Int32)PacketHeaderOffset.OP_CODE;
        CheckSize(@this, offs, sizeof(System.UInt16));
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
    public static PacketFlags ReadFlagsLE(this System.ReadOnlySpan<System.Byte> @this)
    {
        const System.Int32 offs = (System.Int32)PacketHeaderOffset.FLAGS;
        CheckSize(@this, offs, sizeof(System.Byte));
        return (PacketFlags)@this[offs];
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
    public static PacketPriority ReadPriorityLE(this System.ReadOnlySpan<System.Byte> @this)
    {
        const System.Int32 offs = (System.Int32)PacketHeaderOffset.PRIORITY;
        CheckSize(@this, offs, sizeof(System.Byte));
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
    public static ProtocolType ReadTransportLE(this System.ReadOnlySpan<System.Byte> @this)
    {
        const System.Int32 offs = (System.Int32)PacketHeaderOffset.TRANSPORT;
        CheckSize(@this, offs, sizeof(System.Byte));
        return (ProtocolType)@this[offs];
    }

    #endregion

    #region Byte[] convenience overloads

    /// <summary>
    /// Reads the 32‑bit <c>MagicNumber</c> (little‑endian) from a <see cref="System.Byte"/> array.
    /// </summary>
    /// <param name="this">The source array.</param>
    /// <returns>The magic number.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt32 ReadMagicNumberLE(this System.Byte[] @this) => @this.AsReadOnlySpan().ReadMagicNumberLE();

    /// <summary>
    /// Reads the 16‑bit <c>OpCode</c> (little‑endian) from a <see cref="System.Byte"/> array.
    /// </summary>
    /// <param name="this">The source array.</param>
    /// <returns>The operation code.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt16 ReadOpCodeLE(this System.Byte[] @this) => @this.AsReadOnlySpan().ReadOpCodeLE();

    /// <summary>
    /// Reads the <see cref="PacketFlags"/> from a <see cref="System.Byte"/> array.
    /// </summary>
    /// <param name="this">The source array.</param>
    /// <returns>The packet flags.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static PacketFlags ReadFlagsLE(this System.Byte[] @this) => @this.AsReadOnlySpan().ReadFlagsLE();

    /// <summary>
    /// Reads the <see cref="PacketPriority"/> from a <see cref="System.Byte"/> array.
    /// </summary>
    /// <param name="this">The source array.</param>
    /// <returns>The packet priority.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static PacketPriority ReadPriorityLE(this System.Byte[] @this) => @this.AsReadOnlySpan().ReadPriorityLE();

    /// <summary>
    /// Reads the <see cref="ProtocolType"/> from a <see cref="System.Byte"/> array.
    /// </summary>
    /// <param name="this">The source array.</param>
    /// <returns>The transport protocol.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ProtocolType ReadTransportLE(this System.Byte[] @this) => @this.AsReadOnlySpan().ReadTransportLE();

    #endregion

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
    private static void CheckSize(System.ReadOnlySpan<System.Byte> buffer, System.Int32 offset, System.Int32 size)
    {
        if ((System.UInt32)offset > (System.UInt32)buffer.Length ||
            (System.UInt32)size > (System.UInt32)(buffer.Length - offset))
        {
            throw new System.ArgumentException($"Buffer is too small to read {size} bytes at offset {offset}.");
        }
    }

    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.ReadOnlySpan<System.Byte> AsReadOnlySpan(this System.Byte[] buffer) => buffer;

    #endregion
}
