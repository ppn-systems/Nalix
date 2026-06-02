// Copyright (c) 2026 PPN Corporation. All rights reserved.
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
/// Represents the server's response to SessionInit, providing its ephemeral key, nonce, and proof.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("SESSION_CHALLENGE")]
public sealed partial class SessionChallenge : PacketBase<SessionChallenge>, IFixedSizeSerializable, IPacketValidatable
{
    /// <summary>
    /// Gets or sets the server's ephemeral public key.
    /// </summary>
    [SerializeOrder(0)]
    public Bytes32 PublicKey { get; set; }

    /// <summary>
    /// Gets or sets the server's nonce.
    /// </summary>
    [SerializeOrder(1)]
    public Bytes32 Nonce { get; set; }

    /// <summary>
    /// Gets or sets the server's challenge proof.
    /// </summary>
    [SerializeOrder(2)]
    public Bytes32 Proof { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="SessionChallenge"/>.
    /// </summary>
    public SessionChallenge() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with the specified fields.
    /// </summary>
    public void Initialize(Bytes32 publicKey, Bytes32 nonce, Bytes32 proof, PacketFlags flags = PacketFlags.SYSTEM)
    {
        this.OpCode = (ushort)ProtocolOpCode.SYSTEM_CONTROL;
        this.Priority = PacketPriority.URGENT;
        this.Flags = flags;
        this.PublicKey = publicKey;
        this.Nonce = nonce;
        this.Proof = proof;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.PublicKey = Bytes32.Zero;
        this.Nonce = Bytes32.Zero;
        this.Proof = Bytes32.Zero;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = (ushort)ProtocolOpCode.SYSTEM_CONTROL;
        this.Flags = PacketFlags.SYSTEM;
    }

    /// <inheritdoc/>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        if (this.PublicKey.IsZero || this.Nonce.IsZero || this.Proof.IsZero)
        {
            failureReason = "PublicKey, Nonce, and Proof cannot be zero.";
            return false;
        }

        failureReason = null;
        return true;
    }
}
