﻿using Nalix.Common.Package;
using Nalix.Common.Package.Enums;

namespace Nalix.Network.Package;

/// <summary>
/// Represents an immutable network packet with metadata and payload.
/// This high-performance struct is optimized for efficient serialization and transmission.
/// </summary>
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly partial struct Packet : IPacket, System.IDisposable
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="opCode">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(System.UInt16 opCode, System.ReadOnlySpan<System.Byte> payload)
        : this(opCode, CopyToMemory(payload))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="opCode">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(System.UInt16 opCode, System.ReadOnlyMemory<System.Byte> payload)
        : this(opCode, PacketType.Binary, PacketFlags.None, PacketPriority.Low, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for flags and priority.
    /// </summary>
    /// <param name="opCode">The packet opCode.</param>
    /// <param name="s">The packet payload as a UTF-8 encoded string.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(System.UInt16 opCode, System.String s)
        : this(opCode, PacketType.String, PacketFlags.None, PacketPriority.Low, Utf.GetBytes(s))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with type, flags, priority, opCode, and payload.
    /// </summary>
    /// <param name="opCode">The packet opCode.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(
        System.UInt16 opCode,
        System.Byte type,
        System.Byte flags,
        System.Byte priority,
        System.ReadOnlyMemory<System.Byte> payload)
        : this(opCode, (PacketType)type, (PacketFlags)flags, (PacketPriority)priority, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for type, flags, and priority.
    /// </summary>
    /// <param name="opCode">The packet opCode.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Packet(
        System.UInt16 opCode,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        System.ReadOnlyMemory<System.Byte> payload)
        : this(opCode, 0, 0, 0, type, flags, priority, payload)
    {
    }

    #endregion Constructors

    #region Private Methods

    private static System.ReadOnlyMemory<System.Byte> CopyToMemory(System.ReadOnlySpan<System.Byte> span)
    {
        if (span.Length == 0)
        {
            return System.ReadOnlyMemory<System.Byte>.Empty;
        }

        // Tối ưu: chỉ alloc khi cần
        System.Byte[] buffer = new System.Byte[span.Length];
        span.CopyTo(buffer);
        return new System.ReadOnlyMemory<System.Byte>(buffer);
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases any resources used by this packet, returning rented arrays to the pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _buffer.Dispose();

    #endregion IDisposable
}