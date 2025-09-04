// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Protocols;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;

namespace Nalix.Common.Packets.Abstractions;

/// <summary>
/// Defines the common contract for all network packets.
/// </summary>
/// <remarks>
/// An <see cref="IPacket"/> implementation represents a unit of data that can be serialized
/// for transmission or storage and later deserialized for processing.
/// This interface also inherits from <see cref="IPoolable"/> to allow packet pooling.
/// </remarks>
[SerializePackable(SerializeLayout.Explicit)]
public interface IPacket : IPoolable
{
    #region Metadata

    /// <summary>
    /// Gets the total size, in bytes, of the serialized packet, including headers and payload.
    /// </summary>
    [SerializeIgnore]
    System.UInt16 Length { get; }

    /// <summary>
    /// Gets the magic number that uniquely identifies the packet format or protocol.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.MagicNumber)]
    System.UInt32 MagicNumber { get; }

    /// <summary>
    /// Gets the operation code (OpCode) that specifies the command or type of the packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.OpCode)]
    System.UInt16 OpCode { get; }

    /// <summary>
    /// Gets the flags associated with the packet, indicating its state or processing options.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Flags)]
    PacketFlags Flags { get; }

    /// <summary>
    /// Gets the priority level of the packet for processing or transmission.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Priority)]
    PacketPriority Priority { get; }

    /// <summary>
    /// Gets the transport protocol (for example, TCP or UDP) used to transmit the packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Transport)]
    TransportProtocol Transport { get; }

    #endregion Metadata

    #region Packet Methods

    /// <summary>
    /// Serializes the packet into a new byte array.
    /// </summary>
    /// <returns>
    /// A new byte array containing the serialized form of the packet.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    System.Byte[] Serialize();

    /// <summary>
    /// Serializes the packet into the specified destination buffer.
    /// </summary>
    /// <param name="buffer">
    /// The destination buffer where the serialized packet will be written.
    /// The buffer must be large enough to hold the complete packet.
    /// </param>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void Serialize(System.Span<System.Byte> buffer);

    #endregion Packet Methods
}
