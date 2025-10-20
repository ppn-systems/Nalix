// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Shared.Frames.Controls;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[PipelineManagedTransform]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("HANDSHAKE OPCODE={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Handshake : PacketBase<Handshake>
{
    /// <summary>
    /// Suggested minimum granularity for allocation.
    /// </summary>
    public const int DynamicSize = 32;

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeDynamicSize(DynamicSize)]
    [SerializeOrder(PacketHeaderOffset.Region + 1)]
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// Initializes a new <see cref="Handshake"/> with empty content.
    /// </summary>
    public Handshake() => ResetForPool();

    /// <summary>
    /// Initializes a new instance with the specified operation code, binary data, and protocol.
    /// </summary>
    /// <param name="opCode"></param>
    /// <param name="data"></param>
    /// <param name="transport"></param>
    public Handshake(ushort opCode, byte[] data, ProtocolType transport = ProtocolType.TCP) : this()
    {
        Data = data ?? [];
        OpCode = opCode;
        Protocol = transport;
    }

    /// <summary>
    /// Initializes the packet with binary data and an optional transport protocol.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="transport"></param>
    public void Initialize(byte[] data, ProtocolType transport = ProtocolType.TCP)
    {
        Data = data ?? [];
        Protocol = transport;
    }

    /// <summary>
    /// Returns a string representation including all relevant fields.
    /// </summary>
    public override string ToString() => $"HANDSHAKE(OpCode={OpCode}, Length={Length}, Flags={Flags}, Priority={Priority}, Protocol={Protocol}, Data={Data?.Length ?? 0} bytes)";

    /// <summary>
    /// Resets this instance to its default state for pooling reuse.
    /// </summary>
    public override void ResetForPool()
    {
        base.ResetForPool(); // always call for consistency!

        Data = [];
    }
}
