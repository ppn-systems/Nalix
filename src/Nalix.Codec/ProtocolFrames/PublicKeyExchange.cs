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
/// Represents a one-way packet sent from the Server to the Client containing the Server's static public key.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("PUBLIC_KEY_EXCHANGE")]
public sealed partial class PublicKeyExchange : PacketBase<PublicKeyExchange>, IFixedSizeSerializable, IPacketValidatable
{
    /// <summary>
    /// Gets or sets the server's public key.
    /// </summary>
    [SerializeOrder(0)]
    public Bytes32 PublicKey { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicKeyExchange"/> packet.
    /// </summary>
    public PublicKeyExchange() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with the specified public key.
    /// </summary>
    public void Initialize(Bytes32 publicKey)
    {
        this.OpCode = (ushort)ProtocolOpCode.KEY_EXCHANGE;
        this.Priority = PacketPriority.URGENT;
        this.Flags = PacketFlags.SYSTEM;
        this.PublicKey = publicKey;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();

        this.PublicKey = Bytes32.Zero;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = (ushort)ProtocolOpCode.KEY_EXCHANGE;
        this.Flags = PacketFlags.SYSTEM;
    }

    /// <inheritdoc/>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        if (this.PublicKey.IsZero)
        {
            failureReason = "Public key cannot be zero.";
            return false;
        }

        failureReason = null;
        return true;
    }
}
