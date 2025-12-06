// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Shared.Messaging;

/// <summary>
/// Represents the base class for all packet frames in the messaging system.
/// Provides common header fields and serialization logic for derived packet types.
/// </summary>
[MagicNumber(ProtocolMagic.NONE)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public abstract class FrameBase : IPacket
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore] public abstract System.UInt16 Length { get; }

    /// <summary>
    /// Gets the magic number used to identify the packet format.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.MAGIC_NUMBER)] public System.UInt32 MagicNumber { get; set; }

    /// <summary>
    /// Gets the operation code (OpCode) of this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.OP_CODE)] public System.UInt16 OpCode { get; set; }

    /// <summary>
    /// Gets the flags associated with this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.FLAGS)] public PacketFlags Flags { get; set; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.PRIORITY)] public PacketPriority Priority { get; set; }

    /// <summary>
    /// Gets the transport protocol (e.g., TCP/UDP) this packet targets.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.TRANSPORT)] public ProtocolType Protocol { get; set; }

    System.UInt16 IPacket.Length => this.Length;

    /// <inheritdoc/>
    public abstract System.Byte[] Serialize();

    /// <inheritdoc/>
    public abstract System.Int32 Serialize(System.Span<System.Byte> buffer);

    /// <inheritdoc/>
    public abstract void ResetForPool();
}
