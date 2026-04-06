// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Random;
using Nalix.Framework.Security.Asymmetric;
using Nalix.Framework.Security.Primitives;
using Nalix.Network.Protocols;

namespace Nalix.Network.Internal.Protocols;

/// <summary>
/// Implements the default server-side X25519 handshake protocol for Nalix.
/// </summary>
/// <remarks>
/// This protocol is designed to be executed inside a protocol pipeline.
/// It does not own event subscription or disposal of event args.
/// </remarks>
[DebuggerDisplay("Accepting={IsAccepting}, KeepConnectionOpen={KeepConnectionOpen}")]
internal sealed class ProtocolX25519 : Protocol
{
    internal const string StateAttributeKey = "nalix.handshake.state";
    internal const string EstablishedAttributeKey = "nalix.handshake.established";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolX25519"/> class.
    /// </summary>
    public ProtocolX25519()
    {
        this.IsAccepting = true;
        this.KeepConnectionOpen = true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // If already established, do not process handshake frames anymore.
        if (IsEstablished(args.Connection))
        {
            return;
        }

        if (args.Lease is null)
        {
            // No payload => nothing to handshake.
            return;
        }

        try
        {
            Handshake handshake = Handshake.Deserialize(args.Lease.Span);
            this.HandleHandshake(args.Connection, handshake);
        }
        catch { }
    }

    /// <inheritdoc />
    public static bool IsEstablished(IConnection connection)
    {
        if (connection.Attributes.TryGetValue(EstablishedAttributeKey, out object? boxed) &&
            boxed is bool established)
        {
            return established;
        }

        return false;
    }

    private void HandleHandshake(IConnection connection, Handshake handshake)
    {
        switch (handshake.Stage)
        {
            case HandshakeStage.CLIENT_HELLO:
                this.HandleClientHello(connection, handshake);
                return;

            case HandshakeStage.CLIENT_FINISH:
                this.HandleClientFinish(connection, handshake);
                return;

            case HandshakeStage.NONE:
            case HandshakeStage.SERVER_HELLO:
            case HandshakeStage.SERVER_FINISH:
                this.Reject(connection, ProtocolReason.UNEXPECTED_MESSAGE);
                return;

            case HandshakeStage.ERROR:
                connection.Disconnect("Handshake error received from peer.");
                return;

            default:
                this.Reject(connection, ProtocolReason.UNEXPECTED_MESSAGE);
                return;
        }
    }

    private void HandleClientHello(IConnection connection, Handshake packet)
    {
        if (!HandshakeCrypto.IsValid(packet))
        {
            this.Reject(connection, ProtocolReason.MALFORMED_PACKET);
            return;
        }

        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        byte[] sharedSecret = X25519.Agreement(serverKey.PrivateKey, packet.PublicKey);

        if (HandshakeCrypto.IsAllZero(sharedSecret))
        {
            this.Reject(connection, ProtocolReason.DECRYPTION_FAILED);
            return;
        }

        byte[] serverNonce = Csprng.GetBytes(Handshake.DynamicSize);

        byte[] transcriptHash = Handshake.ComputeTranscriptHash(
            HandshakeCrypto.ComposeTranscriptBuffer(
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
            SessionKey = HandshakeCrypto.DeriveSessionKey(sharedSecret, packet.Nonce, serverNonce, transcriptHash)
        };

        connection.Attributes[StateAttributeKey] = state;

        Handshake reply = new(
            packet.OpCode,
            HandshakeStage.SERVER_HELLO,
            serverKey.PublicKey,
            serverNonce,
            HandshakeCrypto.ComputeServerProof(sharedSecret, transcriptHash),
            packet.Protocol)
        {
            TranscriptHash = transcriptHash
        };

        connection.TCP.Send(reply);
    }

    private void HandleClientFinish(IConnection connection, Handshake packet)
    {
        if (!TryGetState(connection, out HandshakeSessionState? state) || state is null)
        {
            this.Reject(connection, ProtocolReason.STATE_VIOLATION);
            return;
        }

        if (packet.Proof.Length != Handshake.DynamicSize || packet.TranscriptHash.Length != Handshake.DynamicSize)
        {
            this.Reject(connection, ProtocolReason.MALFORMED_PACKET);
            return;
        }

        if (!BitwiseOperations.FixedTimeEquals(packet.TranscriptHash, state.TranscriptHash))
        {
            this.Reject(connection, ProtocolReason.CHECKSUM_FAILED);
            return;
        }

        byte[] expectedProof = HandshakeCrypto.ComputeClientProof(state.SharedSecret, state.TranscriptHash);

        if (!BitwiseOperations.FixedTimeEquals(packet.Proof, expectedProof))
        {
            this.Reject(connection, ProtocolReason.SIGNATURE_INVALID);
            return;
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
            HandshakeCrypto.ComputeServerFinishProof(state.SharedSecret, state.TranscriptHash),
            packet.Protocol)
        {
            TranscriptHash = state.TranscriptHash,
            SessionToken = (Snowflake)connection.ID
        };

        connection.TCP.Send(reply);

        // Do NOT unsubscribe from events here.
        // The protocol pipeline controls routing after establishment.
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

    private static bool TryGetState(IConnection connection, out HandshakeSessionState? state)
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
