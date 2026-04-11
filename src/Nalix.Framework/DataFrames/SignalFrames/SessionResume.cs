// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Time;

namespace Nalix.Framework.DataFrames.SignalFrames;

/// <summary>
/// Represents a client request to resume a previously established session.
/// </summary>
[ExcludeFromCodeCoverage]
[SerializePackable(SerializeLayout.Sequential)]
[DebuggerDisplay("SESSION_RESUME Token={SessionToken}, Ts={Timestamp}, OpCode={OpCode}")]
public sealed class SessionResume : PacketBase<SessionResume>, IFixedSizeSerializable
{
    /// <inheritdoc/>
    [SerializeIgnore]
    public static int Size => PacketConstants.HeaderSize + Snowflake.Size + sizeof(long);

    /// <summary>
    /// Gets or sets the session token to resume.
    /// </summary>
    [SerializeOrder(0)]
    public Snowflake SessionToken { get; set; }

    /// <summary>
    /// Gets or sets the client timestamp used for diagnostics and replay hardening.
    /// </summary>
    [SerializeOrder(1)]
    public long Timestamp { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionResume"/> packet.
    /// </summary>
    public SessionResume() => this.ResetForPool();

    /// <summary>
    /// Initializes the packet with the supplied token.
    /// </summary>
    /// <param name="sessionToken">The session token to resume.</param>
    /// <param name="transport">The transport protocol.</param>
    public void Initialize(Snowflake sessionToken, ProtocolType transport = ProtocolType.TCP)
    {
        this.OpCode = (ushort)ProtocolOpCode.SESSION_RESUME;
        this.Protocol = transport;
        this.Priority = PacketPriority.URGENT;
        this.SessionToken = sessionToken;
        this.Timestamp = Clock.UnixMillisecondsNow();
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();
        this.OpCode = (ushort)ProtocolOpCode.SESSION_RESUME;
        this.Protocol = ProtocolType.TCP;
        this.Priority = PacketPriority.URGENT;
        this.SessionToken = Snowflake.Empty;
        this.Timestamp = 0;
    }
}
