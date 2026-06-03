// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Traversal.Protocols;

namespace Nalix.Traversal.Packets;

/// <summary>
/// Packet used by clients to acknowledge a successful NAT probe.
/// Receiving this confirms that the UDP hole punch was successful.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class NatProbeAck : PacketBase<NatProbeAck>, IFixedSizeSerializable
{
    /// <summary>
    /// The ID of the peer acknowledging the probe.
    /// </summary>
    [SerializeOrder(0)]
    public ulong PeerId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatProbeAck"/>.
    /// </summary>
    public NatProbeAck() => this.ResetForPool();

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = (ushort)TraversalOpcode.NatProbeAck;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.UNRELIABLE;
        this.PeerId = 0;
    }
}
