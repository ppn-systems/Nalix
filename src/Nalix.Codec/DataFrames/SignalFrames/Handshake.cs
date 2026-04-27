// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Codec.Security.Hashing;
using Nalix.Common.Identity;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Primitives;
using Nalix.Common.Serialization;

namespace Nalix.Codec.DataFrames.SignalFrames;

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
[Packet]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("HANDSHAKE Stage={Stage}, OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Handshake : PacketBase<Handshake>, IFixedSizeSerializable, IPacketValidatable
{
    /// <summary>
    /// Default dynamic size hint used for fixed-width handshake fields.
    /// </summary>
    [SerializeIgnore]
    public const int DynamicSize = 32;

    /// <inheritdoc/>
    [SerializeIgnore]
    public static int Size { get; } =
        PacketConstants.HeaderSize +
        sizeof(HandshakeStage) +    // Stage
        sizeof(ProtocolReason) +    // Reason
        Bytes32.Size +             // PublicKey
        Bytes32.Size +             // Nonce
        Bytes32.Size +             // Proof
        Bytes32.Size +             // TranscriptHash
        ISnowflake.Size;             // SessionToken

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
    public ISnowflake SessionToken { get; set; } = default!;

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
        this.SessionToken = ISnowflake.Empty!;
    }

    /// <summary>
    /// Initializes the handshake packet with an error state and reason.
    /// </summary>
    public void InitializeError(ProtocolReason reason, PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE)
    {
        this.OpCode = (ushort)ProtocolOpCode.HANDSHAKE;
        this.Stage = HandshakeStage.ERROR;
        this.Priority = PacketPriority.URGENT;
        this.Flags = flags;
        this.Reason = reason;

        this.PublicKey = Bytes32.Zero;
        this.Nonce = Bytes32.Zero;
        this.Proof = Bytes32.Zero;
        this.TranscriptHash = Bytes32.Zero;
        this.SessionToken = ISnowflake.Empty!;
    }

    /// <inheritdoc/>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        if (this == null)
        {
            failureReason = "Handshake packet is null.";
            return false;
        }

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
    /// Computes the Keccak-256 transcript hash for the provided bytes.
    /// </summary>
    /// <param name="transcript">Handshake transcript bytes.</param>
    /// <returns>A 32-byte Keccak-256 hash.</returns>
    [return: NotNull]
    public static Bytes32 ComputeTranscriptHash(ReadOnlySpan<byte> transcript)
        => Keccak256.HashDataToFixed(transcript);

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
           $"Flags={this.Flags}, Priority={this.Priority}, SessionToken={this.SessionToken})";

    /// <summary>
    /// Resets this instance for safe pool reuse.
    /// </summary>
    public override void ResetForPool()
    {
        base.ResetForPool();

        this.OpCode = (ushort)ProtocolOpCode.HANDSHAKE;
        this.Flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE;
        this.Stage = HandshakeStage.NONE;
        this.Reason = ProtocolReason.NONE;
        this.PublicKey = Bytes32.Zero;
        this.Nonce = Bytes32.Zero;
        this.Proof = Bytes32.Zero;
        this.TranscriptHash = Bytes32.Zero;
        this.SessionToken = ISnowflake.Empty!;
        this.Priority = PacketPriority.URGENT;
    }
}
