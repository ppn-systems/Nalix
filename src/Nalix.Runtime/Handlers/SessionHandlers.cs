// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.Pooling;
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
    public static async ValueTask HandleAsync(IPacketContext<SessionResume> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Hub is null)
        {
            await HandleFailureAsync(context.Connection, ProtocolReason.SERVICE_UNAVAILABLE).ConfigureAwait(false);
            return;
        }

        SessionResume packet = context.Packet;
        if (packet.Stage != SessionResumeStage.REQUEST)
        {
            return;
        }

        if (packet.SessionToken.IsEmpty)
        {
            await HandleFailureAsync(context.Connection, ProtocolReason.TOKEN_REVOKED).ConfigureAwait(false);
            return;
        }

        SessionEntry? session = await Hub.SessionStore.RetrieveAsync(packet.SessionToken.ToUInt56())
                                                      .ConfigureAwait(false);
        if (session == null)
        {
            await HandleFailureAsync(context.Connection, ProtocolReason.SESSION_EXPIRED).ConfigureAwait(false);
            return;
        }

        using PacketLease<SessionResume> lease = PacketPool<SessionResume>.Rent();
        SessionResume ack = lease.Value;
        ack.Initialize(
            stage: SessionResumeStage.RESPONSE,
            sessionToken: Snowflake.NewId(session.Snapshot.SessionToken),
            reason: ProtocolReason.NONE,
            transport: packet.Protocol);

        await context.Connection.TCP.SendAsync(ack).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a failure acknowledgement and disconnects the connection.
    /// </summary>
    /// <param name="connection">The connection to close.</param>
    /// <param name="reason">The failure reason to report.</param>
    private static async ValueTask HandleFailureAsync(IConnection connection, ProtocolReason reason)
    {
        using PacketLease<SessionResume> lease = PacketPool<SessionResume>.Rent();
        SessionResume ack = lease.Value;
        ack.Initialize(
            stage: SessionResumeStage.RESPONSE,
            sessionToken: default,
            reason: reason,
            transport: ProtocolType.TCP);

        try
        {
            await connection.TCP.SendAsync(ack)
                                .ConfigureAwait(false);
        }
        finally
        {
            connection.Disconnect($"Session resume rejected: {reason}");
        }
    }
}
