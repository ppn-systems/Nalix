// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Framework.DataFrames.SignalFrames;

/// <summary>
/// Represents a handshake packet used during connection setup.
/// </summary>
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Auto)]
[DebuggerDisplay("HANDSHAKE OPCODE={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Handshake : PacketBase<Handshake>
{
    /// <summary>
    /// Gets the default dynamic size hint for handshake payloads.
    /// </summary>
    public const int DynamicSize = 32;

    /// <summary>
    /// Gets or sets the handshake payload.
    /// </summary>
    [SerializeDynamicSize(DynamicSize)]
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the authentication payload for the handshake.
    /// </summary>
    [SkipClean]
    public HandshakeAuth Auth { get; set; } = new HandshakeAuth();

    /// <summary>
    /// Gets or sets the optional identity string.
    /// </summary>
    public string Identity { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new <see cref="Handshake"/> with empty content.
    /// </summary>
    public Handshake() => this.ResetForPool();

    /// <summary>Initializes a new instance with the specified opcode, payload, and transport.</summary>
    /// <param name="opCode">The packet opcode.</param>
    /// <param name="data">The handshake payload.</param>
    /// <param name="transport">The transport protocol.</param>
    public Handshake(ushort opCode, byte[] data, ProtocolType transport = ProtocolType.TCP) : this()
    {
        this.Data = data ?? Array.Empty<byte>();
        this.OpCode = opCode;
        this.Protocol = transport;
    }

    /// <summary>Initializes the packet with payload and authentication data.</summary>
    /// <param name="opCode">The packet opcode.</param>
    /// <param name="data">The handshake payload.</param>
    /// <param name="publicKey">The authentication public key.</param>
    /// <param name="signature">The authentication signature.</param>
    /// <param name="transport">The transport protocol.</param>
    public void Initialize(ushort opCode, byte[] data, byte[] publicKey, byte[] signature, ProtocolType transport = ProtocolType.TCP)
    {
        this.OpCode = opCode;
        this.Protocol = transport;

        this.Data = data ?? Array.Empty<byte>();
        this.Auth.Signature = signature ?? Array.Empty<byte>();
        this.Auth.PublicKey = publicKey ?? Array.Empty<byte>();
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

    /// <summary>Authentication information for this handshake.</summary>
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
