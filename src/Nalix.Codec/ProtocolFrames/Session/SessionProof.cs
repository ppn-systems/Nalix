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
/// Represents the client's confirmation of the derived transcript and proof of possession.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("SESSION_PROOF")]
public sealed partial class SessionProof : PacketBase<SessionProof>, IFixedSizeSerializable, IPacketValidatable
{
    /// <summary>
    /// Gets or sets the client's proof.
    /// </summary>
    [SerializeOrder(0)]
    public Bytes32 Proof { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="SessionProof"/>.
    /// </summary>
    public SessionProof() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with the specified proof.
    /// </summary>
    public void Initialize(Bytes32 proof, PacketFlags flags = PacketFlags.SYSTEM)
    {
        this.OpCode = (ushort)ProtocolOpCode.SESSION_PROOF;
        this.Priority = PacketPriority.URGENT;
        this.Flags = flags;
        this.Proof = proof;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.Proof = Bytes32.Zero;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = (ushort)ProtocolOpCode.SESSION_PROOF;
        this.Flags = PacketFlags.SYSTEM;
    }

    /// <inheritdoc/>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        if (this.Proof.IsZero)
        {
            failureReason = "Proof cannot be zero.";
            return false;
        }

        failureReason = null;
        return true;
    }
}
