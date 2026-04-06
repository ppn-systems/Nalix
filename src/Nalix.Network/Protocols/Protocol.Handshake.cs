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

namespace Nalix.Network.Protocols;

/// <summary>
/// Implements the default server-side X25519 handshake protocol for Nalix.
/// </summary>
/// <remarks>
/// This protocol performs an anonymous Elliptic Curve Diffie-Hellman (ECDH) handshake using Curve25519. 
/// It involves:
/// <list type="bullet">
/// <item>Receiving a CLIENT_HELLO with the client's public key and nonce.</item>
/// <item>Responding with a SERVER_HELLO containing the server's ephemeral public key, nonce, and a proof.</item>
/// <item>Verifying the client's CLIENT_FINISH proof.</item>
/// <item>Deriving a shared session key used for symmetric encryption (ChaCha20Poly1305) after establishment.</item>
/// </list>
/// Once established, the handshake protocol unbinds itself from the connection's processing pipeline.
/// </remarks>
[DebuggerDisplay("Accepting={IsAccepting}, KeepConnectionOpen={KeepConnectionOpen}")]
public sealed class ProtocolX25519 : Protocol
{
    #region Properties

    private const string StateAttributeKey = "nalix.handshake.state";
    private const string EstablishedAttributeKey = "nalix.handshake.established";

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolX25519"/> class.
    /// </summary>
    public ProtocolX25519()
    {
        this.IsAccepting = true;
        this.KeepConnectionOpen = true;
    }

    #endregion Constructor

    #region Public APIs

    /// <summary>
    /// Binds this handshake protocol to a connection's processing event.
    /// It will automatically unbind upon a successful handshake.
    /// </summary>
    public void Bind(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.OnProcessEvent += this.ProcessMessage;
    }

    #endregion Public APIs

    #region Overrides

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            if (args.Lease is null)
            {
                return;
            }

            Handshake handshake = Handshake.Deserialize(args.Lease.Span);

            this.HandleHandshake(args.Connection, handshake);
        }
        finally
        {
            args.Lease?.Dispose();
            args.Dispose();
        }
    }

    #endregion Overrides

    #region Private Methods

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
        // Verify the length and format of the client Key and Nonce
        if (!HandshakeCrypto.IsValid(packet))
        {
            this.Reject(connection, ProtocolReason.MALFORMED_PACKET);
            return;
        }

        // Generate ephemeral server keypair and derive shared secret (ECDH X25519)
        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        byte[] sharedSecret = X25519.Agreement(serverKey.PrivateKey, packet.PublicKey);
        if (HandshakeCrypto.IsAllZero(sharedSecret))
        {
            this.Reject(connection, ProtocolReason.DECRYPTION_FAILED);
            return;
        }

        byte[] serverNonce = Csprng.GetBytes(Handshake.DynamicSize);
        // Compute the transcript hash to authenticate the handshake state
        byte[] transcriptHash = Handshake.ComputeTranscriptHash(HandshakeCrypto
                                         .ComposeTranscriptBuffer(packet.PublicKey, packet.Nonce, serverKey.PublicKey, serverNonce));

        // Save session context into Connection Attributes to verify at the FINISH stage
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

        Handshake reply = new(packet.OpCode, HandshakeStage.SERVER_HELLO, serverKey.PublicKey, serverNonce, HandshakeCrypto.ComputeServerProof(sharedSecret, transcriptHash), packet.Protocol)
        {
            TranscriptHash = transcriptHash
        };

        // Send SERVER_HELLO response for the client to process
        connection.TCP.Send(reply);
    }

    private void HandleClientFinish(IConnection connection, Handshake packet)
    {
        // State must have been created during the HELLO stage
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

        // Use a fixed-time equality check to prevent timing attacks
        if (!BitwiseOperations.FixedTimeEquals(packet.Proof, expectedProof))
        {
            this.Reject(connection, ProtocolReason.SIGNATURE_INVALID);
            return;
        }

        // Handshake successful - Assign session key and enable CipherSuite context on the connection
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
        connection.OnProcessEvent -= this.ProcessMessage;
    }

    private void Reject(IConnection connection, ProtocolReason reason)
    {
        try
        {
            // Remove handshake event subscriber before rejecting the connection
            connection.OnProcessEvent -= this.ProcessMessage;

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
        if (connection.Attributes.TryGetValue(StateAttributeKey, out object? boxed) && boxed is HandshakeSessionState typed)
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

    #endregion Private Methods
}
