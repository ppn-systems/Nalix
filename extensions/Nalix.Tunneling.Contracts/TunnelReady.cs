// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Tunneling.Protocols;

namespace Nalix.Tunneling;

/// <summary>
/// Packet sent from Provider to Server to authenticate a new data connection.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class TunnelReady : PacketBase<TunnelReady>, IFixedSizeSerializable
{
    [SerializeOrder(0)]
    public Bytes32 Token { get; set; }

    public TunnelReady() => this.ResetForPool();

    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = (ushort)TunnelOpCode.TunnelReady;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.RELIABLE;
        this.Token = default;
    }
}
