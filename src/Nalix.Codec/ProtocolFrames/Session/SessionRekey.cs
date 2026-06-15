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
/// Represents a session rekey packet, used to reestablish a session after a disconnect or network interruption.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("SESSION_REKEY")]
public sealed partial class SessionRekey : PacketBase<SessionRekey>, IFixedSizeSerializable, IPacketValidatable, IPacketStaticOpcode
{
    /// <inheritdoc/>
    public static ushort StaticOpCode => (ushort)ProtocolOpCode.SESSION_REKEY;

    /// <summary>
    /// Gets or sets the public key of the server.
    /// </summary>
    [SerializeOrder(0)]
    public Bytes32 PublicKey { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionRekey"/> packet.
    /// </summary>
    public SessionRekey() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with a public key.
    /// </summary>
    /// <param name="publicKey">The public key to send.</param>
    public void Initialize(Bytes32 publicKey)
    {
        this.PublicKey = publicKey;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.SYSTEM;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();

        this.PublicKey = Bytes32.Zero;
        this.Priority = PacketPriority.HIGH;
        this.Flags = PacketFlags.SYSTEM;
    }

    /// <inheritdoc/>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        if (this.PublicKey.IsZero)
        {
            failureReason = "PublicKey cannot be zero.";
            return false;
        }

        failureReason = null;
        return true;
    }
}
