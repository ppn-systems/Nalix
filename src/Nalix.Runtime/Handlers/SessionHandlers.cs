// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.Pooling;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Security.Hashing;

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

        if (context.Connection.Attributes.ContainsKey(ConnectionAttributes.HandshakeEstablished))
        {
            await HandleFailureAsync(context.Connection, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        SessionResume packet = context.Packet;

        if (!packet.Validate(out string? reason))
        {
            Debug.WriteLine($"[NW.Session] Rejecting SESSION_RESUME. Reason: {reason}");
            await HandleFailureAsync(context.Connection, ProtocolReason.MALFORMED_PACKET).ConfigureAwait(false);
            return;
        }

        if (packet.Stage != SessionResumeStage.REQUEST)
        {
            return;
        }

        // SEC-33: Use ConsumeAsync for atomic retrieve-and-remove to prevent TOCTOU race.
        // Two parallel requests with the same token: only the first gets the entry,
        // the second gets null because TryRemove is atomic.
        SessionEntry? session = await Hub.SessionStore.ConsumeAsync(packet.SessionToken.ToUInt56())
                                                       .ConfigureAwait(false);
        if (session == null)
        {
            await HandleFailureAsync(context.Connection, ProtocolReason.SESSION_EXPIRED).ConfigureAwait(false);
            return;
        }

        // SEC-16: Validate proof-of-possession (MAC) using the stored session secret.
        // We compute HMAC-SHA256(Secret, SessionToken) and compare it with the client's proof.
        // This ensures the client knows the secret without sending it over the wire.
        if (session.Snapshot.Secret.IsZero)
        {
            session.Return();
            await HandleFailureAsync(context.Connection, ProtocolReason.TOKEN_REVOKED).ConfigureAwait(false);
            return;
        }

        Span<byte> expectedProofBytes = stackalloc byte[32];
        Span<byte> tokenBytes = stackalloc byte[8];
        _ = packet.SessionToken.TryWriteBytes(tokenBytes);

        // SEC-16: Use fast HMAC instead of slow PBKDF2 for session resumption to prevent DoS.
        HmacKeccak256.Compute(session.Snapshot.Secret.AsSpan(), tokenBytes[..7], expectedProofBytes);

        Bytes32 expectedProof = new(expectedProofBytes);
        if (packet.Proof != expectedProof)
        {
            session.Return();
            await HandleFailureAsync(context.Connection, ProtocolReason.TOKEN_REVOKED).ConfigureAwait(false);
            return;
        }

        // Token was already consumed atomically by ConsumeAsync — no separate RemoveAsync needed.

        ApplySession(context.Connection, session);

        // Generate and store a new session entry with a rotated token for subsequent resume attempts.
        SessionEntry newEntry = Hub.SessionStore.CreateSession(context.Connection);
        await Hub.SessionStore.StoreAsync(newEntry).ConfigureAwait(false);
        UInt56 newToken = newEntry.Snapshot.SessionToken;

        using PacketLease<SessionResume> lease = PacketPool<SessionResume>.Rent();
        SessionResume ack = lease.Value;
        ack.Initialize(
            stage: SessionResumeStage.RESPONSE,
            sessionToken: Snowflake.NewId(newToken),
            reason: ProtocolReason.NONE,
            flags: packet.Flags);

        await context.Connection.TCP.SendAsync(ack).ConfigureAwait(false);
        session.Return();
    }

    /// <summary>
    /// Restores the saved session snapshot onto the live connection before acknowledging resume.
    /// </summary>
    /// <param name="connection">The connection being resumed.</param>
    /// <param name="session">The stored session entry.</param>
    private static void ApplySession(IConnection connection, SessionEntry session)
    {
        SessionSnapshot snapshot = session.Snapshot;

        connection.Secret = snapshot.Secret;
        connection.Algorithm = snapshot.Algorithm;
        connection.Level = snapshot.Level;

        if (snapshot.Attributes is not null)
        {
            foreach (KeyValuePair<string, object> attribute in snapshot.Attributes)
            {
                connection.Attributes[attribute.Key] = attribute.Value;
            }
        }

        connection.Attributes[ConnectionAttributes.HandshakeEstablished] = true;
        connection.Attributes[ConnectionAttributes.SessionToken] = snapshot.SessionToken;
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
            flags: PacketFlags.SYSTEM | (connection.TCP != null ? PacketFlags.RELIABLE : PacketFlags.UNRELIABLE));

        try
        {
            await connection.TCP!.SendAsync(ack)
                                 .ConfigureAwait(false);
        }
        finally
        {
            connection.Disconnect($"Session resume rejected: {reason}");
        }
    }
}
