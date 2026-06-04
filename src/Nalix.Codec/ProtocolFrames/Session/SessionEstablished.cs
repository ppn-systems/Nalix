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
/// Represents the server's acknowledgment of handshake completion and session establishment.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("SESSION_ESTABLISHED Token={SessionToken}")]
public sealed partial class SessionEstablished : PacketBase<SessionEstablished>, IFixedSizeSerializable, IPacketValidatable, IPacketStaticOpcode
{
    /// <inheritdoc/>
    public static ushort StaticOpCode => (ushort)ProtocolOpCode.SESSION_ESTABLISHED;

    /// <summary>
    /// Gets or sets the server's final finish proof.
    /// </summary>
    [SerializeOrder(0)]
    public Bytes32 Proof { get; set; }

    /// <summary>
    /// Gets or sets the assigned session token.
    /// </summary>
    [SerializeOrder(1)]
    public ulong SessionToken { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="SessionEstablished"/>.
    /// </summary>
    public SessionEstablished() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with the specified fields.
    /// </summary>
    public void Initialize(Bytes32 proof, ulong sessionToken, PacketFlags flags = PacketFlags.SYSTEM)
    {

        this.Priority = PacketPriority.URGENT;
        this.Flags = flags;
        this.Proof = proof;
        this.SessionToken = sessionToken;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.Proof = Bytes32.Zero;
        this.SessionToken = 0;
        this.Priority = PacketPriority.URGENT;

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
