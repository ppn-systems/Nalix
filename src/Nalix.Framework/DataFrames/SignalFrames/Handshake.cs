// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.Security.Hashing;

namespace Nalix.Framework.DataFrames.SignalFrames;

/// <summary>
/// Identifies the current phase of the default Nalix handshake flow.
/// </summary>
public enum HandshakeStage : byte
{
    /// <summary>
    /// No handshake stage is assigned.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// Client starts the handshake and sends its ephemeral public key.
    /// </summary>
    CLIENT_HELLO = 0x01,

    /// <summary>
    /// Server responds with its ephemeral public key and proof.
    /// </summary>
    SERVER_HELLO = 0x02,

    /// <summary>
    /// Client confirms the derived transcript and proves possession.
    /// </summary>
    CLIENT_FINISH = 0x03,

    /// <summary>
    /// Server acknowledges handshake completion.
    /// </summary>
    SERVER_FINISH = 0x04,

    /// <summary>
    /// Handshake failed and the payload carries failure proof or diagnostics.
    /// </summary>
    ERROR = 0xFF
}

/// <summary>
/// Represents the default protocol handshake packet for key exchange and transcript verification.
/// </summary>
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("HANDSHAKE Stage={Stage}, OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Handshake : PacketBase<Handshake>
{
    /// <summary>
    /// Default dynamic size hint used for fixed-width handshake fields.
    /// </summary>
    public const int DynamicSize = 32;

    /// <summary>
    /// Gets or sets the handshake stage carried by this packet.
    /// </summary>
    [SerializeOrder(0)]
    public HandshakeStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the ephemeral public key for the current handshake side.
    /// X25519 public keys are expected to be 32 bytes.
    /// </summary>
    [SerializeOrder(1)]
    [SerializeDynamicSize(DynamicSize)]
    public byte[] PublicKey { get; set; } = [];

    /// <summary>
    /// Gets or sets the handshake nonce or challenge bytes.
    /// </summary>
    [SerializeOrder(2)]
    [SerializeDynamicSize(DynamicSize)]
    public byte[] Nonce { get; set; } = [];

    /// <summary>
    /// Gets or sets the proof bytes for the current stage.
    /// This is typically a MAC or transcript-derived verifier.
    /// </summary>
    [SerializeOrder(3)]
    [SerializeDynamicSize(DynamicSize)]
    public byte[] Proof { get; set; } = [];

    /// <summary>
    /// Gets or sets the Keccak-256 transcript hash associated with the handshake state.
    /// </summary>
    [SerializeOrder(4)]
    [SerializeDynamicSize(DynamicSize)]
    public byte[] TranscriptHash { get; set; } = [];

    /// <summary>
    /// Initializes a new <see cref="Handshake"/> with default transport metadata.
    /// </summary>
    public Handshake() => this.ResetForPool();

    /// <summary>
    /// Initializes a new handshake packet with the specified stage and payload components.
    /// </summary>
    /// <param name="opCode">The opcode identifying the handler for the stage.</param>
    /// <param name="stage">The current handshake stage.</param>
    /// <param name="publicKey">The ephemeral public key for this message.</param>
    /// <param name="nonce">The stage nonce or challenge bytes.</param>
    /// <param name="proof">Optional proof bytes for this stage.</param>
    /// <param name="transport">The transport protocol.</param>
    public Handshake(
        ushort opCode,
        HandshakeStage stage,
        byte[] publicKey,
        byte[] nonce,
        byte[]? proof = null,
        ProtocolType transport = ProtocolType.TCP) : this()
        => this.Initialize(opCode, stage, publicKey, nonce, proof, transport);

    /// <summary>
    /// Initializes the handshake packet with the supplied stage data.
    /// </summary>
    /// <param name="opCode">The opcode identifying the handler for the stage.</param>
    /// <param name="stage">The current handshake stage.</param>
    /// <param name="publicKey">The ephemeral public key.</param>
    /// <param name="nonce">The nonce or challenge bytes.</param>
    /// <param name="proof">Optional proof bytes.</param>
    /// <param name="transport">The transport protocol.</param>
    public void Initialize(
        ushort opCode,
        HandshakeStage stage,
        byte[] publicKey,
        byte[] nonce,
        byte[]? proof = null,
        ProtocolType transport = ProtocolType.TCP)
    {
        this.OpCode = opCode;
        this.Stage = stage;
        this.Protocol = transport;
        this.Priority = PacketPriority.URGENT;

        this.PublicKey = publicKey ?? Array.Empty<byte>();
        this.Nonce = nonce ?? Array.Empty<byte>();
        this.Proof = proof ?? Array.Empty<byte>();
        this.TranscriptHash = Array.Empty<byte>();
    }

    /// <summary>
    /// Computes the Keccak-256 transcript hash for the provided bytes.
    /// </summary>
    /// <param name="transcript">Handshake transcript bytes.</param>
    /// <returns>A 32-byte Keccak-256 hash.</returns>
    [return: NotNull]
    public static byte[] ComputeTranscriptHash(ReadOnlySpan<byte> transcript)
        => Keccak256.HashData(transcript);

    /// <summary>
    /// Recomputes and stores the Keccak-256 transcript hash for this packet.
    /// </summary>
    /// <param name="transcript">Handshake transcript bytes.</param>
    public void UpdateTranscriptHash(ReadOnlySpan<byte> transcript)
        => this.TranscriptHash = ComputeTranscriptHash(transcript);

    /// <summary>
    /// Returns a compact debug representation of this handshake packet.
    /// </summary>
    public override string ToString()
        => $"HANDSHAKE(Stage={this.Stage}, OpCode={this.OpCode}, Length={this.Length}, " +
           $"Flags={this.Flags}, Priority={this.Priority}, Protocol={this.Protocol}, " +
           $"Pub={this.PublicKey.Length}, Nonce={this.Nonce.Length}, Proof={this.Proof.Length}, Hash={this.TranscriptHash.Length})";

    /// <summary>
    /// Resets this instance for safe pool reuse.
    /// </summary>
    public override void ResetForPool()
    {
        base.ResetForPool();

        this.Stage = HandshakeStage.NONE;
        this.PublicKey = [];
        this.Nonce = [];
        this.Proof = [];
        this.TranscriptHash = [];
        this.Priority = PacketPriority.URGENT;
    }
}
