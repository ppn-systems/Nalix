// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Common.Serialization;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Time;

namespace Nalix.Framework.DataFrames.SignalFrames;

/// <summary>
/// Represents the server response for a session resume attempt.
/// </summary>
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Sequential)]
[DebuggerDisplay("SESSION_RESUME_ACK Success={Success}, Reason={Reason}, Token={SessionToken}")]
public sealed class SessionResumeAck : PacketBase<SessionResumeAck>, IFixedSizeSerializable
{
    /// <inheritdoc/>
    [SerializeIgnore]
    public static int Size => PacketConstants.HeaderSize
        + sizeof(bool)
        + sizeof(ProtocolReason)
        + Snowflake.Size
        + sizeof(CipherSuiteType)
        + sizeof(PermissionLevel)
        + sizeof(long);

    /// <summary>
    /// Gets or sets whether the resume attempt succeeded.
    /// </summary>
    [SerializeOrder(0)]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the protocol reason for the outcome.
    /// </summary>
    [SerializeOrder(1)]
    public ProtocolReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the token the client should keep using.
    /// </summary>
    [SerializeOrder(2)]
    public Snowflake SessionToken { get; set; }

    /// <summary>
    /// Gets or sets the cipher suite restored for the connection.
    /// </summary>
    [SerializeOrder(3)]
    public CipherSuiteType Algorithm { get; set; }

    /// <summary>
    /// Gets or sets the permission level restored for the connection.
    /// </summary>
    [SerializeOrder(4)]
    public PermissionLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the server timestamp of the acknowledgement.
    /// </summary>
    [SerializeOrder(5)]
    public long Timestamp { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionResumeAck"/> packet.
    /// </summary>
    public SessionResumeAck() => this.ResetForPool();

    /// <summary>
    /// Initializes the acknowledgement packet.
    /// </summary>
    /// <param name="success">Whether the resume succeeded.</param>
    /// <param name="reason">The protocol reason.</param>
    /// <param name="sessionToken">The token to keep using.</param>
    /// <param name="algorithm">The restored cipher suite.</param>
    /// <param name="level">The restored permission level.</param>
    /// <param name="transport">The transport protocol.</param>
    public void Initialize(
        bool success,
        ProtocolReason reason,
        Snowflake sessionToken,
        CipherSuiteType algorithm,
        PermissionLevel level,
        ProtocolType transport = ProtocolType.TCP)
    {
        this.OpCode = (ushort)ProtocolOpCode.SESSION_RESUME;
        this.Protocol = transport;
        this.Priority = PacketPriority.URGENT;
        this.Success = success;
        this.Reason = reason;
        this.SessionToken = sessionToken;
        this.Algorithm = algorithm;
        this.Level = level;
        this.Timestamp = Clock.UnixMillisecondsNow();
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = (ushort)ProtocolOpCode.SESSION_RESUME;
        this.Protocol = ProtocolType.TCP;
        this.Priority = PacketPriority.URGENT;
        this.Success = false;
        this.Reason = ProtocolReason.NONE;
        this.SessionToken = Snowflake.Empty;
        this.Algorithm = CipherSuiteType.Chacha20Poly1305;
        this.Level = PermissionLevel.NONE;
        this.Timestamp = 0;
    }
}
