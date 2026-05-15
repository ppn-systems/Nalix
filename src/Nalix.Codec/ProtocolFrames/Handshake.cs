// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Codec.ProtocolFrames;

/// <summary>
/// Represents the default protocol handshake packet for key exchange and transcript verification.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("HANDSHAKE Stage={Stage}, OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed partial class Handshake : PacketBase<Handshake>, IFixedSizeSerializable, IPacketValidatable
{
    /// <summary>
    /// Stages the current phase of the handshake process.
    /// </summary>
    [SerializeOrder(0)]
    public HandshakeStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the protocol reason code (used primarily in error responses).
    /// </summary>
    [SerializeOrder(1)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the session token assigned by the server.
    /// Used primarily for UDP connection mapping.
    /// </summary>
    [SerializeOrder(2)]
    public ulong SessionToken { get; set; }

    /// <summary>
    /// Gets or sets the ephemeral public key for the current handshake side.
    /// X25519 public keys are expected to be 32 bytes.
    /// </summary>
    [SerializeOrder(3)]
    public Bytes32 PublicKey { get; set; }

    /// <summary>
    /// Gets or sets the handshake nonce or challenge bytes.
    /// </summary>
    [SerializeOrder(4)]
    public Bytes32 Nonce { get; set; }

    /// <summary>
    /// Gets or sets the proof bytes for the current stage.
    /// This is typically a MAC or transcript-derived verifier.
    /// </summary>
    [SerializeOrder(5)]
    public Bytes32 Proof { get; set; }

    /// <summary>
    /// Gets or sets the Keccak-256 transcript hash associated with the handshake state.
    /// </summary>
    [SerializeOrder(6)]
    public Bytes32 TranscriptHash { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Handshake"/> with default transport metadata.
    /// </summary>
    public Handshake() => this.ResetForPool();

    /// <summary>
    /// Initializes a new handshake packet with the specified stage and payload components.
    /// </summary>
    /// <param name="stage">The current handshake stage.</param>
    /// <param name="publicKey">The ephemeral public key for this message.</param>
    /// <param name="nonce">The stage nonce or challenge bytes.</param>
    /// <param name="proof">Optional proof bytes for this stage.</param>
    /// <param name="flags">The transport reliability flags.</param>
    public Handshake(
        HandshakeStage stage,
        Bytes32 publicKey,
        Bytes32 nonce,
        Bytes32? proof = null,
        PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE) : this()
        => this.Initialize(stage, publicKey, nonce, proof, flags);

    /// <summary>
    /// Initializes the handshake packet with the supplied stage data.
    /// </summary>
    /// <param name="stage">The current handshake stage.</param>
    /// <param name="publicKey">The ephemeral public key.</param>
    /// <param name="nonce">The nonce or challenge bytes.</param>
    /// <param name="proof">Optional proof bytes.</param>
    /// <param name="flags">The transport reliability flags.</param>
    public void Initialize(
        HandshakeStage stage,
        Bytes32 publicKey,
        Bytes32 nonce,
        Bytes32? proof = null,
        PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE)
    {
        this.OpCode = (ushort)ProtocolOpCode.HANDSHAKE;
        this.Stage = stage;
        this.Priority = PacketPriority.URGENT;
        this.Flags = flags;

        this.Reason = ProtocolReason.NONE;
        this.PublicKey = publicKey;
        this.Nonce = nonce;
        this.Proof = proof ?? Bytes32.Zero;
        this.TranscriptHash = Bytes32.Zero;
        this.SessionToken = 0;
    }

    /// <summary>
    /// Initializes the handshake packet with an error state and reason.
    /// </summary>
    public void InitializeError(ProtocolReason reason, PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE)
    {
        this.Flags = flags;
        this.Reason = reason;
        this.Stage = HandshakeStage.ERROR;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = (ushort)ProtocolOpCode.HANDSHAKE;

        this.SessionToken = 0;
        this.Nonce = Bytes32.Zero;
        this.Proof = Bytes32.Zero;
        this.PublicKey = Bytes32.Zero;
        this.TranscriptHash = Bytes32.Zero;
    }

    /// <inheritdoc/>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        bool isValid = this.Stage switch
        {
            HandshakeStage.CLIENT_HELLO =>
                !this.PublicKey.IsZero && !this.Nonce.IsZero && this.Proof.IsZero && this.TranscriptHash.IsZero,

            HandshakeStage.SERVER_HELLO =>
                !this.PublicKey.IsZero && !this.Nonce.IsZero && !this.Proof.IsZero && !this.TranscriptHash.IsZero,

            HandshakeStage.CLIENT_FINISH =>
                this.PublicKey.IsZero && this.Nonce.IsZero && !this.Proof.IsZero && !this.TranscriptHash.IsZero,

            HandshakeStage.SERVER_FINISH =>
                this.PublicKey.IsZero && this.Nonce.IsZero && !this.Proof.IsZero && !this.TranscriptHash.IsZero,

            HandshakeStage.ERROR or HandshakeStage.NONE =>
                this.Reason != ProtocolReason.NONE,
            _ => false
        };

        if (!isValid)
        {
            failureReason = $"Invalid cryptographic fields or structural anomaly detected for stage {this.Stage}.";
            return false;
        }

        failureReason = null;
        return true;
    }

    /// <summary>
    /// Returns a compact debug representation of this handshake packet.
    /// </summary>
    public override string ToString()
        => $"HANDSHAKE(Stage={this.Stage}, OpCode={this.OpCode}, Length={this.Length}, " +
           $"Flags={this.Flags}, Priority={this.Priority}, SessionToken={this.SessionToken})";

    /// <summary>
    /// Resets this instance for safe pool reuse.
    /// </summary>
    public override void ResetForPool()
    {
        base.ResetForPool();

        this.SessionToken = 0;
        this.Nonce = Bytes32.Zero;
        this.Proof = Bytes32.Zero;
        this.PublicKey = Bytes32.Zero;
        this.Stage = HandshakeStage.NONE;
        this.Reason = ProtocolReason.NONE;
        this.TranscriptHash = Bytes32.Zero;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = (ushort)ProtocolOpCode.HANDSHAKE;
        this.Flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE;
    }
}

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
