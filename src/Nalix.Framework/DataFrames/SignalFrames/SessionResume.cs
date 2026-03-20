// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Primitives;
using Nalix.Common.Serialization;
using Nalix.Framework.Identifiers;

namespace Nalix.Framework.DataFrames.SignalFrames;

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
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Sequential)]
[DebuggerDisplay("SESSION_SIGNAL Stage={Stage}, Token={SessionToken}, Reason={Reason}")]
public sealed class SessionResume : PacketBase<SessionResume>, IFixedSizeSerializable
{
    /// <inheritdoc/>
    [SerializeIgnore]
    public static int Size => PacketConstants.HeaderSize
        + sizeof(SessionResumeStage)
        + Snowflake.Size
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
    public Snowflake SessionToken { get; set; }

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
    /// Initializes a new instance of the <see cref="SessionResume"/> packet.
    /// </summary>
    public SessionResume() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with the specified stage and metadata.
    /// </summary>
    public void Initialize(SessionResumeStage stage, Snowflake sessionToken, ProtocolReason reason = ProtocolReason.NONE, ProtocolType transport = ProtocolType.TCP, Bytes32 proof = default)
    {
        this.OpCode = (ushort)ProtocolOpCode.SESSION_SIGNAL;
        this.Protocol = transport;
        this.Priority = PacketPriority.URGENT;
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
        this.Protocol = ProtocolType.TCP;
        this.Priority = PacketPriority.URGENT;
        this.Stage = SessionResumeStage.NONE;
        this.SessionToken = Snowflake.Empty;
        this.Reason = ProtocolReason.NONE;
        this.Proof = Bytes32.Zero;
    }
}
