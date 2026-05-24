// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Codec.ProtocolFrames;

/// <summary>
/// Represents a packet used for custom payload size and fragment benchmarking.
/// </summary>
[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Sequential)]
[DebuggerDisplay("BenchmarkPacket Seq={SequenceId}, OpCode={OpCode}, PayloadLength={Payload?.Length ?? 0}")]
public sealed partial class BenchmarkPacket : PacketBase<BenchmarkPacket>
{
    /// <summary>
    /// Gets or sets the custom payload content.
    /// </summary>
    [SerializeOrder(0)]
    public byte[]? Payload { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkPacket"/> class.
    /// </summary>
    public BenchmarkPacket()
    {
        this.OpCode = 0x0100; // User-space opcode
        this.Priority = PacketPriority.HIGH;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.Payload = null;
        this.OpCode = 0x0100;
        this.Priority = PacketPriority.HIGH;
    }
}
