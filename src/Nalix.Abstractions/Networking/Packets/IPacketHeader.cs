// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Primitives;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Represents a lightweight, zero-copy view over a packet header.
/// </summary>
/// <remarks>
/// This interface exposes direct access to individual header fields without requiring
/// allocation or copying of a <see cref="PacketHeader"/> instance.
/// 
/// It is intended for high-performance scenarios where packet implementations
/// store header data internally and need to provide efficient field access.
/// </remarks>
public interface IPacketHeader
{
    /// <summary>
    /// Gets or sets the protocol magic number used to identify valid packets.
    /// </summary>
    uint MagicNumber { get; set; }

    /// <summary>
    /// Gets or sets the operation code that defines the packet type or action.
    /// </summary>
    ushort OpCode { get; set; }

    /// <summary>
    /// Gets or sets the flags that modify packet behavior.
    /// </summary>
    PacketFlags Flags { get; set; }

    /// <summary>
    /// Gets or sets the priority level of the packet.
    /// </summary>
    PacketPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the sequence identifier used for ordering or tracking packets.
    /// </summary>
    ushort SequenceId { get; set; }
}
