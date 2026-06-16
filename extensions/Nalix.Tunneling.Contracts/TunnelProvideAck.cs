// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Tunneling;

/// <summary>
/// Packet sent from Server to Provider acknowledging the channel registration.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class TunnelProvideAck : PacketBase<TunnelProvideAck>, IFixedSizeSerializable, IPacketStaticOpcode
{
    /// <inheritdoc/>
    public static ushort StaticOpCode => (ushort)ProtocolOpCode.TUNNEL_PROVIDE_ACK;

    /// <inheritdoc/>
    [SerializeOrder(0)]
    public bool Success { get; set; }

    /// <inheritdoc/>
    [SerializeOrder(1)]
    public byte Reason { get; set; }

    /// <inheritdoc/>
    public TunnelProvideAck() => this.ResetForPool();

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.NONE;
        this.Success = false;
        this.Reason = 0;
    }
}

