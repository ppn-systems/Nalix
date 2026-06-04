// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Tunneling.Protocols;

namespace Nalix.Tunneling;

/// <summary>
/// Packet sent from Consumer to Server to request a tunnel connection.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class TunnelConnect : PacketBase<TunnelConnect>, IFixedSizeSerializable
{
    [SerializeOrder(0)]
    public ushort ChannelId { get; set; }

    public TunnelConnect() => this.ResetForPool();

    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = (ushort)TunnelOpCode.TunnelConnect;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.RELIABLE;
        this.ChannelId = 0;
    }
}
