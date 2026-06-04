// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Traversal.Packets;

/// <summary>
/// Packet used by clients to actively punch holes through their NAT.
/// Sent repeatedly over UDP to the remote peer's public endpoint.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class NatProbe : PacketBase<NatProbe>, IFixedSizeSerializable, IPacketStaticOpcode
{
    public static ushort StaticOpCode => (ushort)ProtocolOpCode.TRAVERSAL_NAT_PROBE;
    /// <summary>
    /// The ID of the peer sending the probe, used for validation.
    /// </summary>
    [SerializeOrder(0)]
    public ulong PeerId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatProbe"/>.
    /// </summary>
    public NatProbe() => this.ResetForPool();

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();

        this.PeerId = 0;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.UNRELIABLE;
    }
}

