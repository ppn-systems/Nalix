// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Sessions;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Handles dedicated session resume packets.
/// </summary>
[PacketController("Session")]
public sealed class SessionHandlers
{
    /// <summary>
    /// Handles a session resume request and restores the connection state when the token is valid.
    /// </summary>
    /// <param name="context">The typed packet context for the incoming resume packet.</param>
    /// <returns>The acknowledgement packet when the resume succeeds; otherwise <see langword="null"/> after disconnecting.</returns>
    [ReservedOpcodePermitted]
    [PacketOpcode((ushort)ProtocolOpCode.SESSION_RESUME)]
    public static SessionResumeAck? Handle(IPacketContext<SessionResume> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ISessionManager? sessionManager = InstanceManager.Instance.GetExistingInstance<ISessionManager>();
        if (sessionManager is null)
        {
            SendFailureAndDisconnect(context.Connection, ProtocolReason.SERVICE_UNAVAILABLE);
            return null;
        }

        SessionResume packet = context.Packet;
        if (packet.SessionToken.IsEmpty)
        {
            SendFailureAndDisconnect(context.Connection, ProtocolReason.TOKEN_REVOKED);
            return null;
        }

        SessionResumeResult result = sessionManager.TryResume(packet.SessionToken.ToUInt56(), context.Connection);
        if (!result.Success || result.Snapshot is null)
        {
            SendFailureAndDisconnect(context.Connection, result.Reason);
            return null;
        }

        RestoreConnection(context.Connection, result.Snapshot);

        SessionResumeAck ack = new();
        ack.Initialize(
            success: true,
            reason: ProtocolReason.NONE,
            sessionToken: Snowflake.FromUInt56(result.SessionToken),
            algorithm: result.Snapshot.Algorithm,
            level: result.Snapshot.Level,
            transport: packet.Protocol);

        return ack;
    }

    /// <summary>
    /// Restores the live connection state from the supplied snapshot.
    /// </summary>
    /// <param name="connection">The connection receiving the restored state.</param>
    /// <param name="snapshot">The snapshot to copy from.</param>
    private static void RestoreConnection(IConnection connection, SessionSnapshot snapshot)
    {
        connection.Secret = [.. snapshot.Secret];
        connection.Algorithm = snapshot.Algorithm;
        connection.Level = snapshot.Level;

        foreach (KeyValuePair<string, object> item in snapshot.Attributes)
        {
            connection.Attributes[item.Key] = item.Value;
        }

        connection.Attributes[HandshakeHandlers.EstablishedAttributeKey] = true;
    }

    /// <summary>
    /// Sends a failure acknowledgement and disconnects the connection.
    /// </summary>
    /// <param name="connection">The connection to close.</param>
    /// <param name="reason">The failure reason to report.</param>
    private static void SendFailureAndDisconnect(IConnection connection, ProtocolReason reason)
    {
        SessionResumeAck ack = new();
        ack.Initialize(
            success: false,
            reason: reason,
            sessionToken: default,
            algorithm: connection.Algorithm,
            level: connection.Level,
            transport: ProtocolType.TCP);

        try
        {
            connection.TCP.Send(ack);
        }
        finally
        {
            connection.Disconnect($"Session resume rejected: {reason}");
        }
    }
}
