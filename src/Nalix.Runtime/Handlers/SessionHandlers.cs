// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Injection;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Codec.Pooling;
using Nalix.Codec.ProtocolFrames;
using Nalix.Codec.Security.Hashing;
using Nalix.Environment.Time;
using Nalix.Runtime.Extensions;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Handles dedicated session resume packets.
/// </summary>
[PacketHandler("Nalix.Session")]
public static partial class SessionHandlers
{
    [Inject]
    private static ISessionService s_sessionService = null!;

    /// <summary>
    /// Handles a session resume request and restores the connection state when the token is valid.
    /// </summary>
    /// <param name="context">The typed packet context for the incoming session signal.</param>
    /// <returns>The acknowledgement signal when the resume succeeds; otherwise <see langword="null"/> after disconnecting.</returns>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode(ProtocolOpCode.SESSION_SIGNAL)]
    public static async ValueTask HandleAsync(IPacketContext<SessionResume> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsReliable)
        {
            // This is a replayed packet, ignore silently.
            return;
        }

        IConnection connection = context.Connection;
        IConnectionHub? hub = connection.GetHub();

        if (hub is null)
        {
            await HandleFailureAsync(context, ProtocolReason.SERVICE_UNAVAILABLE).ConfigureAwait(false);
            return;
        }

        if (connection.GetRuntimeState().HandshakeEstablished)
        {
            await HandleFailureAsync(context, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        SessionResume packet = context.Packet;

        if (!packet.Validate(out string? reason))
        {
            await HandleFailureAsync(context, ProtocolReason.MALFORMED_PACKET).ConfigureAwait(false);
            return;
        }

        if (packet.Stage != SessionResumeStage.REQUEST)
        {
            return;
        }

        // SEC-33: Use ConsumeAsync for atomic retrieve-and-remove to prevent TOCTOU race.
        // Two parallel requests with the same token: only the first gets the entry,
        // the second gets null because TryRemove is atomic.
        SessionEntry? session = await s_sessionService.ConsumeAsync(packet.SessionToken)
                                                     .ConfigureAwait(false);
        if (session == null)
        {
            await HandleFailureAsync(context, ProtocolReason.SESSION_EXPIRED).ConfigureAwait(false);
            return;
        }

        // SEC-16: Validate proof-of-possession (MAC) using the stored session secret.
        // We compute HMAC-SHA256(Secret, SessionToken) and compare it with the client's proof.
        // This ensures the client knows the secret without sending it over the wire.
        if (session.Snapshot.Secret.IsZero)
        {
            session.Return();
            await HandleFailureAsync(context, ProtocolReason.TOKEN_REVOKED).ConfigureAwait(false);
            return;
        }

        Span<byte> messageBytes = stackalloc byte[16];
        Span<byte> expectedProofBytes = stackalloc byte[32];

        BinaryPrimitives.WriteUInt64LittleEndian(messageBytes, packet.SessionToken);

        long currentWindow = Clock.UnixSecondsNow() / 30;
        bool validProof = false;

        // SEC-16: Validate proof-of-possession (MAC) using the stored session secret and sliding window.
        // We compute HMAC-Keccak256(Secret, SessionToken || TimeWindow) for t-1, t, t+1.
        for (long w = currentWindow - 1; w <= currentWindow + 1; w++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(messageBytes[8..], w);
            HmacKeccak256.Compute(session.Snapshot.Secret.AsSpan(), messageBytes, expectedProofBytes);

            if (packet.Proof == new Bytes32(expectedProofBytes))
            {
                validProof = true;
                break;
            }
        }

        if (!validProof)
        {
            session.Return();
            await HandleFailureAsync(context, ProtocolReason.TOKEN_REVOKED).ConfigureAwait(false);
            return;
        }

        // Token was already consumed atomically by ConsumeAsync — no separate RemoveAsync needed.
        RestoreSessionSnapshot(connection, session);
        connection.GetRuntimeState().HandshakeEstablished = true;

        ConnectionSequenceState seqState = connection.GetSequenceState();

        // Restore sequence number
        if (session.Snapshot.Attributes?.TryGetValue(ConnectionAttributes.SequenceState, out object? seqObj) == true && seqObj is ConnectionSequenceState snapshotSeq)
        {
            seqState.TcpSendSequence = snapshotSeq.TcpSendSequence;
            connection.TCP.SendSequence.ResumeFrom(snapshotSeq.TcpSendSequence);

            seqState.TcpReceiveSequence = snapshotSeq.TcpReceiveSequence;
            connection.TCP.ReceiveSequence.ResumeFrom(snapshotSeq.TcpReceiveSequence);

            if (connection.IsUdpCreated)
            {
                seqState.UdpSendSequence = snapshotSeq.UdpSendSequence;
                connection.UDP!.SendSequence.ResumeFrom(snapshotSeq.UdpSendSequence);

                seqState.UdpReceiveSequence = snapshotSeq.UdpReceiveSequence;
                connection.UDP!.ReceiveSequence.ResumeFrom(snapshotSeq.UdpReceiveSequence);
            }
        }

        await s_sessionService.SaveSessionAsync(connection).ConfigureAwait(false);

        ulong newToken = connection.ConnectionId;

        Span<byte> responseMessageBytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(responseMessageBytes, newToken);
        BinaryPrimitives.WriteInt64LittleEndian(responseMessageBytes[8..], Clock.UnixSecondsNow() / 30);
        Span<byte> responseProofBytes = stackalloc byte[32];
        HmacKeccak256.Compute(session.Snapshot.Secret.AsSpan(), responseMessageBytes, responseProofBytes);

        using PacketScope<SessionResume> lease = PacketFactory<SessionResume>.Acquire();
        SessionResume ack = lease.Value;
        ack.Initialize(
            stage: SessionResumeStage.RESPONSE,
            sessionToken: newToken,
            reason: ProtocolReason.NONE,
            proof: new Bytes32(responseProofBytes),
            flags: packet.Flags);

        await context.Sender.SendAsync(ack).ConfigureAwait(false);
        session.Return();
    }

    /// <summary>
    /// Restores the saved session snapshot onto the live connection before acknowledging resume.
    /// </summary>
    /// <param name="connection">The connection being resumed.</param>
    /// <param name="session">The stored session entry.</param>
    private static void RestoreSessionSnapshot(IConnection connection, SessionEntry session)
    {
        SessionSnapshot snapshot = session.Snapshot;

        connection.Level = snapshot.Level;
        connection.Secret = snapshot.Secret;
        connection.Algorithm = snapshot.Algorithm;

        if (snapshot.Attributes is not null)
        {
            foreach (KeyValuePair<AttributeKey, object> attribute in snapshot.Attributes)
            {
                connection.Attributes[attribute.Key] = attribute.Value;
            }
        }

        connection.GetRuntimeState().HandshakeEstablished = true;
    }

    /// <summary>
    /// Sends a failure acknowledgement and disconnects the connection.
    /// </summary>
    /// <param name="context">The connection to close.</param>
    /// <param name="reason">The failure reason to report.</param>
    private static async ValueTask HandleFailureAsync(IPacketContext<SessionResume> context, ProtocolReason reason)
    {
        using PacketScope<SessionResume> lease = PacketFactory<SessionResume>.Acquire();
        SessionResume ack = lease.Value;
        ack.Initialize(
            stage: SessionResumeStage.RESPONSE,
            sessionToken: default,
            reason: reason,
            flags: PacketFlags.SYSTEM);

        try
        {
            await context.Sender.SendAsync(ack).ConfigureAwait(false);
            await Task.Delay(50).ConfigureAwait(false);
        }
        finally
        {
            context.Connection.Disconnect($"Session resume rejected: {reason}");
        }
    }
}
