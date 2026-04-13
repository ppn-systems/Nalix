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
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.Pooling;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Asymmetric;
using Nalix.Framework.Security.Primitives;

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
        if (!Handshake.IsValid(packet) || packet.PublicKey.Length != X25519.KeySize)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.MALFORMED_PACKET).ConfigureAwait(false);
            return;
        }

        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        byte[] sharedSecret = X25519.Agreement(serverKey.PrivateKey, packet.PublicKey);

        if (BitwiseOperations.IsZero(sharedSecret))
        {
            MemorySecurity.ZeroMemory(sharedSecret);
            MemorySecurity.ZeroMemory(serverKey.PrivateKey);
            await RejectHandshakeAsync(connection, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        byte[] serverNonce = Csprng.GetBytes(Handshake.DynamicSize);

        byte[] transcriptHash = Handshake.ComputeTranscriptHash(
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

        MemorySecurity.ZeroMemory(serverKey.PrivateKey);
        await connection.TCP.SendAsync(reply).ConfigureAwait(false);
    }

    private static async ValueTask HandleClientFinishAsync(IConnection connection, Handshake packet)
    {
        if (!TryGetState(connection, out HandshakeContext? state) || state is null)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        if (packet.Proof.Length != Handshake.DynamicSize || packet.TranscriptHash.Length != Handshake.DynamicSize)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.MALFORMED_PACKET).ConfigureAwait(false);
            return;
        }

        if (!BitwiseOperations.FixedTimeEquals(packet.TranscriptHash, state.TranscriptHash))
        {
            await RejectHandshakeAsync(connection, ProtocolReason.CHECKSUM_FAILED).ConfigureAwait(false);
            return;
        }

        byte[] expectedProof = HandshakeX25519.ComputeClientProof(state.SharedSecret, state.TranscriptHash);
        if (!BitwiseOperations.FixedTimeEquals(packet.Proof, expectedProof))
        {
            MemorySecurity.ZeroMemory(expectedProof);
            await RejectHandshakeAsync(connection, ProtocolReason.SIGNATURE_INVALID).ConfigureAwait(false);
            return;
        }

        MemorySecurity.ZeroMemory(expectedProof);
        connection.Secret = [.. state.SessionKey];
        connection.Algorithm = CipherSuiteType.Chacha20Poly1305;

        connection.Attributes[ConnectionAttributes.HandshakeEstablished] = true;
        _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);

        SessionEntry? session = Hub?.SessionStore.CreateSession(connection);

        using PacketLease<Handshake> lease = PacketPool<Handshake>.Rent();
        Handshake reply = lease.Value;
        reply.Stage = HandshakeStage.SERVER_FINISH;
        reply.PublicKey = Array.Empty<byte>();
        reply.Nonce = Array.Empty<byte>();
        reply.Proof = HandshakeX25519.ComputeServerFinishProof(state.SharedSecret, state.TranscriptHash);
        reply.Protocol = packet.Protocol;
        reply.TranscriptHash = state.TranscriptHash;
        reply.SessionToken = session is not null ? Snowflake.NewId(session.Snapshot.SessionToken) : (Snowflake)connection.ID;

        MemorySecurity.ZeroMemory(state.SessionKey);
        MemorySecurity.ZeroMemory(state.SharedSecret);

        await connection.TCP.SendAsync(reply).ConfigureAwait(false);
    }

    private static async ValueTask RejectHandshakeAsync(IConnection connection, ProtocolReason reason)
    {
        if (TryGetState(connection, out HandshakeContext? state) && state is not null)
        {
            MemorySecurity.ZeroMemory(state.SessionKey);
            MemorySecurity.ZeroMemory(state.SharedSecret);
            _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);
        }

        try
        {
            using PacketLease<Control> lease = PacketPool<Control>.Rent();
            Control control = lease.Value;
            control.Initialize(opCode: 0, type: ControlType.ERROR, reasonCode: reason, transport: ProtocolType.TCP);
            await connection.TCP.SendAsync(control).ConfigureAwait(false);
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
        public byte[] ClientPublicKey { get; init; } = [];
        public byte[] ClientNonce { get; init; } = [];
        public byte[] ServerPublicKey { get; init; } = [];
        public byte[] ServerNonce { get; init; } = [];
        public byte[] SharedSecret { get; init; } = [];
        public byte[] TranscriptHash { get; init; } = [];
        public byte[] SessionKey { get; init; } = [];
    }

    #endregion Private Methods
}
