// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Traversal.Protocols;

namespace Nalix.Traversal.Packets;

/// <summary>
/// Server responds to a ReflectorInit with the allocated Reflector Token and the UDP endpoint.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class ReflectorAllocated : PacketBase<ReflectorAllocated>, IFixedSizeSerializable
{
    /// <summary>
    /// The generated Reflector Token that both peers must use to wrap their UDP datagrams.
    /// </summary>
    [SerializeOrder(0)]
    public ulong ReflectorToken { get; set; }

    /// <summary>
    /// Indicates whether the Reflector allocation was successful.
    /// </summary>
    [SerializeOrder(1)]
    public bool Success { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectorAllocated"/>.
    /// </summary>
    public ReflectorAllocated() => this.ResetForPool();

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = (ushort)TraversalOpcode.ReflectorAllocated;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.RELIABLE;
        this.ReflectorToken = 0;
        this.Success = false;
    }
}
