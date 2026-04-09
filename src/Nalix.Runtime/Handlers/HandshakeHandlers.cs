// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
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
    internal const string StateAttributeKey = "nalix.handshake.state";
    internal const string EstablishedAttributeKey = "nalix.handshake.established";

    /// <summary>
    /// Handles incoming handshake signal packets.
    /// </summary>
    /// <param name="context">The packet context containing the handshake metadata.</param>
    /// <returns>A responding handshake packet or null if rejected/disconnected.</returns>
    [PacketOpcode((ushort)ProtocolOpCode.HANDSHAKE)]
    [ReservedOpcodePermitted]
    public Handshake? HandleHandshake(IPacketContext<Handshake> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Handshake packet = context.Packet;
        IConnection connection = context.Connection;

        switch (packet.Stage)
        {
            case HandshakeStage.CLIENT_HELLO:
                return this.HandleClientHello(connection, packet);

            case HandshakeStage.CLIENT_FINISH:
                return this.HandleClientFinish(connection, packet);

            case HandshakeStage.ERROR:
                connection.Disconnect("Handshake error received from peer.");
                return null;

            case HandshakeStage.NONE:
            case HandshakeStage.SERVER_HELLO:
            case HandshakeStage.SERVER_FINISH:
            default:
                this.Reject(connection, ProtocolReason.UNEXPECTED_MESSAGE);
                return null;
        }
    }

    /// <summary>
    /// Checks if the handshake has been established for a given connection.
    /// </summary>
    public static bool IsEstablished(IConnection connection)
    {
        if (connection.Attributes.TryGetValue(EstablishedAttributeKey, out object? boxed) &&
            boxed is bool established)
        {
            return established;
        }

        return false;
    }

    private Handshake? HandleClientHello(IConnection connection, Handshake packet)
    {
        if (!Handshake.IsValid(packet))
        {
            this.Reject(connection, ProtocolReason.MALFORMED_PACKET);
            return null;
        }

        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        byte[] sharedSecret = X25519.Agreement(serverKey.PrivateKey, packet.PublicKey);

        if (BitwiseOperations.IsZero(sharedSecret))
        {
            this.Reject(connection, ProtocolReason.DECRYPTION_FAILED);
            return null;
        }

        byte[] serverNonce = Csprng.GetBytes(Handshake.DynamicSize);

        byte[] transcriptHash = Handshake.ComputeTranscriptHash(
            HandshakeX25519.ComposeTranscriptBuffer(
                packet.PublicKey,
                packet.Nonce,
                serverKey.PublicKey,
                serverNonce));

        HandshakeSessionState state = new()
        {
            ClientPublicKey = packet.PublicKey,
            ClientNonce = packet.Nonce,
            SharedSecret = sharedSecret,
            ServerNonce = serverNonce,
            ServerPublicKey = serverKey.PublicKey,
            TranscriptHash = transcriptHash,
            SessionKey = HandshakeX25519.DeriveSessionKey(sharedSecret, packet.Nonce, serverNonce, transcriptHash)
        };

        connection.Attributes[StateAttributeKey] = state;

        Handshake reply = new(
            packet.OpCode,
            HandshakeStage.SERVER_HELLO,
            serverKey.PublicKey,
            serverNonce,
            HandshakeX25519.ComputeServerProof(sharedSecret, transcriptHash),
            packet.Protocol)
        {
            TranscriptHash = transcriptHash
        };

        return reply;
    }

    private Handshake? HandleClientFinish(IConnection connection, Handshake packet)
    {
        if (!TryGetState(connection, out HandshakeSessionState? state) || state is null)
        {
            this.Reject(connection, ProtocolReason.STATE_VIOLATION);
            return null;
        }

        if (packet.Proof.Length != Handshake.DynamicSize || packet.TranscriptHash.Length != Handshake.DynamicSize)
        {
            this.Reject(connection, ProtocolReason.MALFORMED_PACKET);
            return null;
        }

        if (!BitwiseOperations.FixedTimeEquals(packet.TranscriptHash, state.TranscriptHash))
        {
            this.Reject(connection, ProtocolReason.CHECKSUM_FAILED);
            return null;
        }

        byte[] expectedProof = HandshakeX25519.ComputeClientProof(state.SharedSecret, state.TranscriptHash);

        if (!BitwiseOperations.FixedTimeEquals(packet.Proof, expectedProof))
        {
            this.Reject(connection, ProtocolReason.SIGNATURE_INVALID);
            return null;
        }

        connection.Secret = state.SessionKey;
        connection.Algorithm = CipherSuiteType.Chacha20Poly1305;

        connection.Attributes[EstablishedAttributeKey] = true;
        _ = connection.Attributes.Remove(StateAttributeKey);

        Handshake reply = new(
            packet.OpCode,
            HandshakeStage.SERVER_FINISH,
            [],
            [],
            HandshakeX25519.ComputeServerFinishProof(state.SharedSecret, state.TranscriptHash),
            packet.Protocol)
        {
            TranscriptHash = state.TranscriptHash,
            SessionToken = (Snowflake)connection.ID
        };

        return reply;
    }

    private void Reject(IConnection connection, ProtocolReason reason)
    {
        try
        {
            Control control = new();
            control.Initialize(opCode: 0, type: ControlType.ERROR, reasonCode: reason, transport: ProtocolType.TCP);
            connection.TCP.Send(control);
        }
        finally
        {
            connection.Disconnect(reason.ToString());
        }
    }

    private static bool TryGetState(IConnection connection, [NotNullWhen(true)] out HandshakeSessionState? state)
    {
        if (connection.Attributes.TryGetValue(StateAttributeKey, out object? boxed) &&
            boxed is HandshakeSessionState typed)
        {
            state = typed;
            return true;
        }

        state = null;
        return false;
    }

    private sealed class HandshakeSessionState
    {
        public byte[] ClientPublicKey { get; init; } = [];
        public byte[] ClientNonce { get; init; } = [];
        public byte[] ServerPublicKey { get; init; } = [];
        public byte[] ServerNonce { get; init; } = [];
        public byte[] SharedSecret { get; init; } = [];
        public byte[] TranscriptHash { get; init; } = [];
        public byte[] SessionKey { get; init; } = [];
    }
}
