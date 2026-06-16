// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

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
public sealed partial class PeerSignal : PacketBase<PeerSignal>, IFixedSizeSerializable, IPacketStaticOpcode
{
    /// <inheritdoc/>
    public static ushort StaticOpCode => (ushort)ProtocolOpCode.TRAVERSAL_PEER_SIGNAL;

    /// <inheritdoc/>
    [SerializeOrder(0)]
    public SignalType Type { get; set; }

    /// <inheritdoc/>
    [SerializeOrder(1)]
    public ushort Port { get; set; }

    /// <inheritdoc/>
    [SerializeOrder(2)]
    public ulong TargetPeerId { get; set; }

    /// <inheritdoc/>
    [SerializeOrder(3)]
    public ulong AddressHigh { get; set; }

    /// <inheritdoc/>
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
        this.Flags = PacketFlags.NONE;
        this.Priority = PacketPriority.NONE;

    }
}
