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
/// Represents the first step of the session handshake where the client sends its ephemeral public key and nonce.
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("SESSION_INIT")]
public sealed partial class SessionInit : PacketBase<SessionInit>, IFixedSizeSerializable, IPacketValidatable
{
    /// <summary>
    /// Gets or sets the client's ephemeral public key.
    /// </summary>
    [SerializeOrder(0)]
    public Bytes32 PublicKey { get; set; }

    /// <summary>
    /// Gets or sets the client's nonce.
    /// </summary>
    [SerializeOrder(1)]
    public Bytes32 Nonce { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="SessionInit"/>.
    /// </summary>
    public SessionInit() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with the specified public key and nonce.
    /// </summary>
    public void Initialize(Bytes32 publicKey, Bytes32 nonce, PacketFlags flags = PacketFlags.SYSTEM)
    {
        this.OpCode = (ushort)ProtocolOpCode.SESSION_INIT;
        this.Priority = PacketPriority.URGENT;
        this.Flags = flags;
        this.PublicKey = publicKey;
        this.Nonce = nonce;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.PublicKey = Bytes32.Zero;
        this.Nonce = Bytes32.Zero;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = (ushort)ProtocolOpCode.SESSION_INIT;
        this.Flags = PacketFlags.SYSTEM;
    }

    /// <inheritdoc/>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        if (this.PublicKey.IsZero || this.Nonce.IsZero)
        {
            failureReason = "PublicKey and Nonce cannot be zero.";
            return false;
        }

        failureReason = null;
        return true;
    }
}
