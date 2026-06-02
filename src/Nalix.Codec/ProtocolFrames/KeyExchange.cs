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
/// Represents a lightweight key exchange packet for Trust-On-First-Use (TOFU).
/// </summary>
[Packet]
[GenerateFormatter]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("KEY_EXCHANGE Stage={Stage}")]
public sealed partial class KeyExchange : PacketBase<KeyExchange>, IFixedSizeSerializable, IPacketValidatable
{
    /// <summary>
    /// Gets or sets the current stage of the key exchange.
    /// </summary>
    [SerializeOrder(0)]
    public KeyExchangeStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the server's public key (only populated in RESPONSE).
    /// </summary>
    [SerializeOrder(1)]
    public Bytes32 PublicKey { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyExchange"/> packet.
    /// </summary>
    public KeyExchange() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with the specified stage and public key.
    /// </summary>
    public void Initialize(KeyExchangeStage stage, Bytes32 publicKey = default)
    {
        this.OpCode = (ushort)ProtocolOpCode.KEY_EXCHANGE;
        this.Priority = PacketPriority.URGENT;
        this.Flags = PacketFlags.SYSTEM;
        this.Stage = stage;
        this.PublicKey = publicKey;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();

        this.Stage = KeyExchangeStage.NONE;
        this.PublicKey = Bytes32.Zero;
        this.Priority = PacketPriority.URGENT;
        this.OpCode = (ushort)ProtocolOpCode.KEY_EXCHANGE;
        this.Flags = PacketFlags.SYSTEM;
    }

    /// <inheritdoc/>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        bool isValid = this.Stage switch
        {
            KeyExchangeStage.REQUEST => this.PublicKey.IsZero,
            KeyExchangeStage.RESPONSE => !this.PublicKey.IsZero,
            KeyExchangeStage.NONE or _ => false
        };

        if (!isValid)
        {
            failureReason = $"Invalid fields provided for key exchange stage {this.Stage}.";
            return false;
        }

        failureReason = null;
        return true;
    }
}

/// <summary>
/// Identifies the stage of a key exchange operation.
/// </summary>
public enum KeyExchangeStage : byte
{
    /// <summary>
    /// No key exchange stage assigned.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// Client requests the server's public key.
    /// </summary>
    REQUEST = 0x01,

    /// <summary>
    /// Server responds with its static public key.
    /// </summary>
    RESPONSE = 0x02
}
