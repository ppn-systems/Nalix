// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Defines a contract for packets that are sequence-aware,
/// typically used for request/response correlation (e.g., PING/PONG, ACK/NACK).
/// </summary>
public interface IPacketSequenced
{
    /// <summary>
    /// Gets the sequence identifier of the packet.
    /// This is used to correlate requests with their responses.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.SequenceId)]
    uint SequenceId { get; }
}
