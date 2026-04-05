// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Framework.DataFrames;

/// <summary>
/// Represents the base class for all packet frames in the messaging system.
/// Provides common header fields and serialization logic for derived packet types.
/// </summary>
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public abstract class FrameBase : IPacket
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore] public abstract int Length { get; }

    /// <summary>
    /// Gets the magic number used to identify the packet format.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.MagicNumber)] public uint MagicNumber { get; set; }

    /// <summary>
    /// Gets the operation code (OpCode) of this packet.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.OpCode)] public ushort OpCode { get; set; }

    /// <summary>
    /// Gets the flags associated with this packet.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.Flags)] public PacketFlags Flags { get; set; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.Priority)] public PacketPriority Priority { get; set; }

    /// <summary>
    /// Gets the transport protocol (e.g., TCP/UDP) this packet targets.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.Transport)] public ProtocolType Protocol { get; set; }

    /// <summary>
    /// Gets the transport protocol (e.g., TCP/UDP) this packet targets.
    /// </summary>
    [SerializeHeader(PacketHeaderOffset.SequenceId)] public uint SequenceId { get; set; }

    /// <inheritdoc/>
    public abstract void ResetForPool();

    /// <inheritdoc/>
    public abstract byte[] Serialize();

    /// <inheritdoc/>
    public abstract int Serialize(Span<byte> buffer);
}
