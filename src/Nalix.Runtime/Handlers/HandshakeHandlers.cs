// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
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
using Nalix.Framework.Random;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Asymmetric;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for the default server-side X25519 handshake protocol.
/// </summary>
[PacketController("Handshake")]
public sealed class HandshakeHandlers
{
    private static IConnectionHub? Hub => InstanceManager.Instance.GetExistingInstance<IConnectionHub>();

    /// <summary>
    /// Handles incoming handshake signal packets.
    /// </summary>
    /// <param name="context">The packet context containing the handshake metadata.</param>
    /// <returns>A responding handshake packet or null if rejected/disconnected.</returns>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode((ushort)ProtocolOpCode.HANDSHAKE)]
    public static async ValueTask HandleAsync(IPacketContext<Handshake> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Handshake packet = context.Packet;
        IConnection connection = context.Connection;

        if (connection.Attributes.ContainsKey(ConnectionAttributes.HandshakeEstablished))
        {
            await RejectHandshakeAsync(connection, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        switch (packet.Stage)
        {
            case HandshakeStage.CLIENT_HELLO:
                await HandleClientHelloAsync(connection, packet).ConfigureAwait(false);
                break;

            case HandshakeStage.CLIENT_FINISH:
                await HandleClientFinishAsync(connection, packet).ConfigureAwait(false);
                break;

            case HandshakeStage.ERROR:
                connection.Disconnect("Handshake error received from peer.");
                break;

            case HandshakeStage.NONE:
            case HandshakeStage.SERVER_HELLO:
            case HandshakeStage.SERVER_FINISH:
            default:
                await RejectHandshakeAsync(connection, ProtocolReason.UNEXPECTED_MESSAGE).ConfigureAwait(false);
                break;
        }
    }

    #region Private Methods

    private static async ValueTask HandleClientHelloAsync(IConnection connection, Handshake packet)
    {
        // BUG-75: Prevent re-entry during active handshake to mitigate CPU DoS
        if (connection.Attributes.ContainsKey(ConnectionAttributes.HandshakeState))
        {
            await RejectHandshakeAsync(connection, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        if (!Handshake.IsValid(packet))
        {
            await RejectHandshakeAsync(connection, ProtocolReason.MALFORMED_PACKET).ConfigureAwait(false);
            return;
        }

        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        Fixed256 sharedSecret = X25519.Agreement(serverKey.PrivateKey, packet.PublicKey);

        if (sharedSecret.IsEmpty)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        Fixed256 serverNonce = new(Csprng.GetBytes(Fixed256.Size));

        Fixed256 transcriptHash = Handshake.ComputeTranscriptHash(
            HandshakeX25519.ComposeTranscriptBuffer(
                packet.PublicKey,
                packet.Nonce,
                serverKey.PublicKey,
                serverNonce));

        HandshakeContext state = new()
        {
            ClientPublicKey = packet.PublicKey,
            ClientNonce = packet.Nonce,
            SharedSecret = sharedSecret,
            ServerNonce = serverNonce,
            ServerPublicKey = serverKey.PublicKey,
            TranscriptHash = transcriptHash,
            SessionKey = HandshakeX25519.DeriveSessionKey(sharedSecret, packet.Nonce, serverNonce, transcriptHash)
        };

        connection.Attributes[ConnectionAttributes.HandshakeState] = state;

        using PacketLease<Handshake> lease = PacketPool<Handshake>.Rent();
        Handshake reply = lease.Value;
        reply.Stage = HandshakeStage.SERVER_HELLO;
        reply.PublicKey = serverKey.PublicKey;
        reply.Nonce = serverNonce;
        reply.Proof = HandshakeX25519.ComputeServerProof(sharedSecret, transcriptHash);
        reply.Protocol = packet.Protocol;
        reply.TranscriptHash = transcriptHash;

        await connection.TCP.SendAsync(reply).ConfigureAwait(false);
    }

    private static async ValueTask HandleClientFinishAsync(IConnection connection, Handshake packet)
    {
        if (!TryGetState(connection, out HandshakeContext? state) || state is null)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        if (packet.Proof.IsEmpty || packet.TranscriptHash.IsEmpty)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.MALFORMED_PACKET).ConfigureAwait(false);
            return;
        }

        Fixed256 expectedProof = HandshakeX25519.ComputeClientProof(state.SharedSecret, state.TranscriptHash);
        if (packet.Proof != expectedProof)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.SIGNATURE_INVALID).ConfigureAwait(false);
            return;
        }

        if (packet.TranscriptHash != state.TranscriptHash)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.CHECKSUM_FAILED).ConfigureAwait(false);
            return;
        }

        connection.Secret = state.SessionKey.ToByteArray();
        connection.Algorithm = CipherSuiteType.Chacha20Poly1305;

        connection.Attributes[ConnectionAttributes.HandshakeEstablished] = true;
        _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);

        SessionEntry? session = Hub?.SessionStore.CreateSession(connection);
        if (session is not null)
        {
            await Hub!.SessionStore.StoreAsync(session).ConfigureAwait(false);
        }

        using PacketLease<Handshake> lease = PacketPool<Handshake>.Rent();
        Handshake reply = lease.Value;
        reply.Stage = HandshakeStage.SERVER_FINISH;
        reply.PublicKey = Fixed256.Empty;
        reply.Nonce = Fixed256.Empty;
        reply.Proof = HandshakeX25519.ComputeServerFinishProof(state.SharedSecret, state.TranscriptHash);
        reply.Protocol = packet.Protocol;
        reply.TranscriptHash = state.TranscriptHash;
        reply.SessionToken = session is not null ? Snowflake.NewId(session.Snapshot.SessionToken) : (Snowflake)connection.ID;

        await connection.TCP.SendAsync(reply).ConfigureAwait(false);
    }

    private static async ValueTask RejectHandshakeAsync(IConnection connection, ProtocolReason reason)
    {
        if (TryGetState(connection, out HandshakeContext? state) && state is not null)
        {
            _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);
        }

        try
        {
            using PacketLease<Handshake> lease = PacketPool<Handshake>.Rent();
            Handshake error = lease.Value;
            error.InitializeError(reason, connection.TCP is not null ? ProtocolType.TCP : ProtocolType.UDP);
            await (connection.TCP ?? connection.UDP).SendAsync(error).ConfigureAwait(false);
        }
        finally
        {
            connection.Disconnect(reason.ToString());
        }
    }

    private static bool TryGetState(IConnection connection, [NotNullWhen(true)] out HandshakeContext? state)
    {
        if (connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeState, out object? boxed) &&
            boxed is HandshakeContext typed)
        {
            state = typed;
            return true;
        }

        state = null;
        return false;
    }

    private sealed class HandshakeContext
    {
        public Fixed256 ClientPublicKey { get; init; }
        public Fixed256 ClientNonce { get; init; }
        public Fixed256 ServerPublicKey { get; init; }
        public Fixed256 ServerNonce { get; init; }
        public Fixed256 SharedSecret { get; init; }
        public Fixed256 TranscriptHash { get; init; }
        public Fixed256 SessionKey { get; init; }
    }

    #endregion Private Methods
}
