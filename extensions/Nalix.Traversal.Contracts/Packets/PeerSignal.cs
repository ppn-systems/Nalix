// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Traversal.Protocols;

namespace Nalix.Traversal.Packets;

/// <summary>
/// Defines the type of signaling message.
/// </summary>
public enum SignalType : byte
{
    Request = 0,
    CandidateOffer = 1,
    Result = 2
}

/// <summary>
/// Packet used for NAT hole punching coordination via the signaling server.
/// Exchanged over the established TCP connection.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class PeerSignal : PacketBase<PeerSignal>, IFixedSizeSerializable
{
    [SerializeOrder(0)]
    public SignalType Type { get; set; }

    [SerializeOrder(1)]
    public ushort Port { get; set; }

    [SerializeOrder(2)]
    public ulong TargetPeerId { get; set; }

    [SerializeOrder(3)]
    public ulong AddressHigh { get; set; }

    [SerializeOrder(4)]
    public ulong AddressLow { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeerSignal"/>.
    /// </summary>
    public PeerSignal() => this.ResetForPool();

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();

        this.Port = 0;
        this.AddressLow = 0;
        this.AddressHigh = 0;
        this.TargetPeerId = 0;
        this.Type = SignalType.Request;
        this.Flags = PacketFlags.RELIABLE;
        this.Priority = PacketPriority.NONE;
        this.OpCode = (ushort)TraversalOpcode.PeerSignal;
    }
}
