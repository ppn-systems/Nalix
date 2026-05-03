// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames.Formatter;
using Nalix.Codec.Serialization;

namespace Nalix.Codec.DataFrames.SignalFrames;

/// <summary>
/// Identifies the stage of a session management operation.
/// </summary>
public enum SessionResumeStage : byte
{
    /// <summary>
    /// No session stage assigned.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// Client requests to resume a session.
    /// </summary>
    REQUEST = 0x01,

    /// <summary>
    /// Server responds to a resume request.
    /// </summary>
    RESPONSE = 0x02
}

/// <summary>
/// Represents a unified signal packet for session management operations.
/// Replaces redundant SessionResume and SessionResumeAck packets.
/// </summary>
[Packet]
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Explicit)]
[DebuggerDisplay("SESSION_SIGNAL Stage={Stage}, Token={SessionToken}, Reason={Reason}")]
public sealed class SessionResume : PacketBase<SessionResume>, IFixedSizeSerializable, IPacketValidatable
{
    /// <inheritdoc/>
    [SerializeIgnore]
    public static int Size { get; } = PacketConstants.HeaderSize
        + sizeof(SessionResumeStage)
        + ISnowflake.Size
        + sizeof(ProtocolReason)
        + 32; // Proof (HMAC-SHA256)

    /// <summary>
    /// Gets or sets the current stage of the session operation.
    /// </summary>
    [SerializeOrder(0)]
    public SessionResumeStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the session token involved in the operation.
    /// </summary>
    [SerializeOrder(1)]
    public ulong SessionToken { get; set; }

    /// <summary>
    /// Gets or sets the protocol reason code (used primarily in responses).
    /// </summary>
    [SerializeOrder(2)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the HMAC-SHA256 proof of session secret possession.
    /// </summary>
    [SerializeOrder(3)]
    public Bytes32 Proof { get; set; }


    /// <summary>
    /// Registers the <see cref="SessionResumeFormatter"/> to optimize serialization performance.
    /// Static constructor ensures zero-allocation type registration at startup, avoiding dynamic lookup overhead.
    /// </summary>
    static SessionResume() => LiteSerializer.Register(new SessionResumeFormatter());

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionResume"/> packet.
    /// </summary>
    public SessionResume() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with the specified stage and metadata.
    /// </summary>
    public void Initialize(SessionResumeStage stage, ulong sessionToken, ProtocolReason reason = ProtocolReason.NONE, Bytes32 proof = default, PacketFlags flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE)
    {
        this.OpCode = (ushort)ProtocolOpCode.SESSION_SIGNAL;
        this.Priority = PacketPriority.URGENT;
        this.Flags = flags;
        this.Stage = stage;
        this.SessionToken = sessionToken;
        this.Reason = reason;
        this.Proof = proof;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = (ushort)ProtocolOpCode.SESSION_SIGNAL;
        this.Priority = PacketPriority.URGENT;
        this.Flags = PacketFlags.SYSTEM | PacketFlags.RELIABLE;
        this.Stage = SessionResumeStage.NONE;
        this.SessionToken = 0;
        this.Reason = ProtocolReason.NONE;
        this.Proof = Bytes32.Zero;
    }

    /// <inheritdoc/>
    public bool Validate([NotNullWhen(false)] out string? failureReason)
    {
        if (this == null)
        {
            failureReason = "SessionResume packet is null.";
            return false;
        }

        bool isValid = this.Stage switch
        {
            SessionResumeStage.REQUEST =>
                !(this.SessionToken == 0) && !this.Proof.IsZero,

            SessionResumeStage.RESPONSE =>
                this.Reason != ProtocolReason.NONE || (!(this.SessionToken == 0) && !this.Proof.IsZero),
            SessionResumeStage.NONE or _ => false
        };

        if (!isValid)
        {
            failureReason = $"Invalid fields provided for session resume stage {this.Stage}.";
            return false;
        }

        failureReason = null;
        return true;
    }
}
