// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Security;
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
    private static IConnectionHub? Hub => InstanceManager.Instance.GetExistingInstance<IConnectionHub>();

    /// <summary>
    /// Handles a session resume request and restores the connection state when the token is valid.
    /// </summary>
    /// <param name="context">The typed packet context for the incoming session signal.</param>
    /// <returns>The acknowledgement signal when the resume succeeds; otherwise <see langword="null"/> after disconnecting.</returns>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode((ushort)ProtocolOpCode.SESSION_SIGNAL)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "NALIX037:Potential allocation in hot path", Justification = "<Pending>")]
    public static SessionSignal? Handle(IPacketContext<SessionSignal> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Hub is null)
        {
            SEND_FAILURE_AND_DISCONNECT(context.Connection, ProtocolReason.SERVICE_UNAVAILABLE);
            return null;
        }

        SessionSignal packet = context.Packet;
        if (packet.Stage != SessionStage.REQUEST)
        {
            return null;
        }

        if (packet.SessionToken.IsEmpty)
        {
            SEND_FAILURE_AND_DISCONNECT(context.Connection, ProtocolReason.TOKEN_REVOKED);
            return null;
        }

        if (!Hub.TryResumeSession(packet.SessionToken.ToUInt56(), context.Connection, out SessionEntry? session))
        {
            SEND_FAILURE_AND_DISCONNECT(context.Connection, ProtocolReason.SESSION_EXPIRED);
            return null;
        }

        SessionSignal ack = new();
        ack.Initialize(
            stage: SessionStage.RESPONSE,
            sessionToken: Snowflake.NewId(session.Snapshot.SessionToken),
            reason: ProtocolReason.NONE,
            transport: packet.Protocol);

        return ack;
    }

    /// <summary>
    /// Sends a failure acknowledgement and disconnects the connection.
    /// </summary>
    /// <param name="connection">The connection to close.</param>
    /// <param name="reason">The failure reason to report.</param>
    private static void SEND_FAILURE_AND_DISCONNECT(IConnection connection, ProtocolReason reason)
    {
        SessionSignal ack = new();
        ack.Initialize(
            stage: SessionStage.RESPONSE,
            sessionToken: default,
            reason: reason,
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
