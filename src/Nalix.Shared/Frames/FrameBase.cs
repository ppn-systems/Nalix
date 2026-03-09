// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Packets.Enums;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;

namespace Nalix.Shared.Frames;

/// <summary>
/// Represents the base class for all packet frames in the messaging system.
/// Provides common header fields and serialization logic for derived packet types.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public abstract class FrameBase : IPacket
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore] public abstract System.UInt16 Length { get; }

    /// <inheritdoc/>
    [SerializeIgnore] System.UInt16 IPacket.Length => this.Length;

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

    /// <inheritdoc/>
    public abstract void ResetForPool();

    /// <inheritdoc/>
    public abstract System.Byte[] Serialize();

    /// <inheritdoc/>
    public abstract System.Int32 Serialize(System.Span<System.Byte> buffer);
}
