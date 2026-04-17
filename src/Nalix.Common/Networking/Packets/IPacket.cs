// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Nalix.Common.Serialization;
namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Describes a packet that can be serialized and sent over the wire.
/// </summary>
/// <remarks>
/// Implementations expose a fixed header and a serialization API that the packet
/// pipeline can use without knowing the concrete packet type.
/// </remarks>
[SerializePackable(SerializeLayout.Explicit)]
public interface IPacket
{
    #region Metadata

    /// <summary>
    /// Gets the total serialized size, in bytes, including headers and payload.
    /// </summary>
    [SerializeIgnore]
    int Length { get; }

    /// <summary>
    /// Gets the magic number that identifies the packet format or protocol.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.MagicNumber)]
    uint MagicNumber { get; set; }

    /// <summary>
    /// Gets the operation code that identifies the packet handler.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.OpCode)]
    ushort OpCode { get; set; }

    /// <summary>
    /// Gets the flags associated with the packet.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.Flags)]
    PacketFlags Flags { get; set; }

    /// <summary>
    /// Gets the priority level of the packet.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.Priority)]
    PacketPriority Priority { get; set; }

    /// <summary>
    /// Gets the sequence identifier used to correlate requests and responses.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.SequenceId)]
    ushort SequenceId { get; }

    #endregion Metadata

    #region Packet Methods

    /// <summary>
    /// Serializes the packet into a new byte array.
    /// </summary>
    /// <returns>
    /// A new byte array containing the serialized form of the packet.
    /// </returns>
    /// <exception cref="Exceptions.SerializationFailureException">Thrown when the packet cannot be serialized by the configured formatter.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no formatter is available for the packet type.</exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    byte[] Serialize();

    /// <summary>
    /// Serializes the packet into the specified destination buffer.
    /// </summary>
    /// <param name="buffer">
    /// The destination buffer where the serialized packet will be written.
    /// The buffer must be large enough to hold the complete packet.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="buffer"/> is too small for the serialized packet.</exception>
    /// <exception cref="Exceptions.SerializationFailureException">Thrown when the packet cannot be serialized by the configured formatter.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no formatter is available for the packet type.</exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int Serialize(Span<byte> buffer);

    #endregion Packet Methods
}
