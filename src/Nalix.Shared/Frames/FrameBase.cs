// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

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
    [SerializeIgnore] public abstract ushort Length { get; }

    /// <inheritdoc/>
    [SerializeIgnore] ushort IPacket.Length => Length;

    /// <summary>
    /// Gets the magic number used to identify the packet format.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.MAGIC_NUMBER)] public uint MagicNumber { get; set; }

    /// <summary>
    /// Gets the operation code (OpCode) of this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.OP_CODE)] public ushort OpCode { get; set; }

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

    /// <summary>
    /// Gets the transport protocol (e.g., TCP/UDP) this packet targets.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.SEQUENCE_ID)] public uint SequenceId { get; set; }

    /// <inheritdoc/>
    public abstract void ResetForPool();

    /// <inheritdoc/>
    public abstract byte[] Serialize();

    /// <inheritdoc/>
    public abstract int Serialize(System.Span<byte> buffer);
}
