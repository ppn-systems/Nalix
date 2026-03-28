// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Framework.DataFrames.SignalFrames;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[PipelineManagedTransform]
[SerializePackable(SerializeLayout.Explicit)]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("HANDSHAKE OPCODE={OpCode}, Length={Length}, Flags={Flags}")]
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
    /// Authentication information for this handshake, if applicable.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 2)]
    public HandshakeAuth Auth { get; set; } = new HandshakeAuth();

    /// <summary>
    /// Identity string for this handshake.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.Region + 3)]
    public string Identity { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new <see cref="Handshake"/> with empty content.
    /// </summary>
    public Handshake() => this.ResetForPool();

    /// <summary>
    /// Initializes a new instance with the specified operation code, binary data, and protocol.
    /// </summary>
    /// <param name="opCode"></param>
    /// <param name="data"></param>
    /// <param name="transport"></param>
    public Handshake(ushort opCode, byte[] data, ProtocolType transport = ProtocolType.TCP) : this()
    {
        this.Data = data ?? Array.Empty<byte>();
        this.OpCode = opCode;
        this.Protocol = transport;
    }

    /// <summary>
    /// Initializes the packet with binary data and an optional transport protocol.
    /// </summary>
    /// <param name="opCode"></param>
    /// <param name="data"></param>
    /// <param name="PublicKey"></param>
    /// <param name="Signature"></param>
    /// <param name="transport"></param>
    public void Initialize(ushort opCode, byte[] data, byte[] PublicKey, byte[] Signature, ProtocolType transport = ProtocolType.TCP)
    {
        this.OpCode = opCode;
        this.Protocol = transport;

        this.Data = data ?? Array.Empty<byte>();
        this.Auth.Signature = Signature ?? Array.Empty<byte>();
        this.Auth.PublicKey = PublicKey ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Returns a string representation including all relevant fields.
    /// </summary>
    public override string ToString() => $"HANDSHAKE(OpCode={this.OpCode}, Length={this.Length}, Flags={this.Flags}, Priority={this.Priority}, Protocol={this.Protocol}, Data={this.Data?.Length ?? 0} bytes)";

    /// <summary>
    /// Resets this instance to its default state for pooling reuse.
    /// </summary>
    public override void ResetForPool()
    {
        base.ResetForPool(); // always call for consistency!

        this.Data = [];
        this.Auth.Signature = [];
        this.Auth.PublicKey = [];
        this.Identity = string.Empty;
    }

    /// <summary>
    /// Authentication information for this handshake, if applicable.
    /// </summary>
    public sealed class HandshakeAuth
    {
        /// <summary>
        /// Public key used for authentication, if applicable.
        /// </summary>
        [SerializeDynamicSize(DynamicSize)]
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Signature corresponding to the public key, if applicable.
        /// </summary>
        [SerializeDynamicSize(DynamicSize * 2)]
        public byte[] Signature { get; set; } = Array.Empty<byte>();
    }
}
