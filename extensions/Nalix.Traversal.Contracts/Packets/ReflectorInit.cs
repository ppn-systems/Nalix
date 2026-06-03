// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Traversal.Protocols;

namespace Nalix.Traversal.Packets;

/// <summary>
/// Client requests the server to allocate a Reflector Session for communicating with TargetPeerId.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class ReflectorInit : PacketBase<ReflectorInit>, IFixedSizeSerializable
{
    /// <summary>
    /// The ID of the peer we want to Reflector data to.
    /// </summary>
    [SerializeOrder(0)]
    public ulong TargetPeerId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectorInit"/>.
    /// </summary>
    public ReflectorInit() => this.ResetForPool();

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = (ushort)TraversalOpcode.ReflectorInit;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.RELIABLE;
        this.TargetPeerId = 0;
    }
}
