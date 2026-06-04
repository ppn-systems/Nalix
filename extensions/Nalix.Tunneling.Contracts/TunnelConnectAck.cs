// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Tunneling.Protocols;

namespace Nalix.Tunneling;

/// <summary>
/// Packet sent from Server to Consumer acknowledging the tunnel connection.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class TunnelConnectAck : PacketBase<TunnelConnectAck>, IFixedSizeSerializable, IPacketStaticOpcode
{
    public static ushort StaticOpCode => (ushort)ProtocolOpCode.TUNNEL_CONNECT_ACK;
    
    [SerializeOrder(0)]
    public bool Success { get; set; }

    [SerializeOrder(1)]
    public byte Reason { get; set; }

    public TunnelConnectAck() => this.ResetForPool();

    public override void ResetForPool()
    {
        base.ResetForPool();
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.RELIABLE;
        this.Success = false;
        this.Reason = 0;
    }
}

