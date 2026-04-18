// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Random;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Asymmetric;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides extension methods for performing cryptographic handshakes over a <see cref="TransportSession"/>.
/// </summary>
public static class HandshakeExtensions
{
    /// <summary>
    /// Performs the client-side X25519 handshake asynchronously over the connected <see cref="TransportSession"/>.
    /// </summary>
    /// <remarks>
    /// This method performs an anonymous Elliptic Curve Diffie-Hellman (ECDH) handshake using Curve25519.
    /// It generates an ephemeral key pair, exchanges public keys with the server, verifies server proofs, 
    /// and derives a shared session key.
    /// Upon a successful handshake, the session's encryption settings (<see cref="TransportOptions.Secret"/>, 
    /// <see cref="TransportOptions.Algorithm"/>, and <see cref="TransportOptions.EncryptionEnabled"/>) 
    /// are automatically updated to enable secure communication using ChaCha20Poly1305.
    /// </remarks>
    /// <param name="session">The connected transport session to perform the handshake on.</param>
    /// <param name="ct">A cancellation token that can be used to abort the handshake process.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="session"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the session is not connected.</exception>
    /// <exception cref="NetworkException">Thrown if the handshake fails due to malformed packets, invalid proofs, or key agreement failures.</exception>
    public static async Task HandshakeAsync(this TransportSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsConnected)
        {
            throw new InvalidOperationException("Session must be connected to perform handshake.");
        }

        X25519.X25519KeyPair clientKey = X25519.GenerateKeyPair();

        Span<byte> clientNonceBytes = stackalloc byte[Handshake.DynamicSize];
        Csprng.Fill(clientNonceBytes);
        Bytes32 clientNonce = new(clientNonceBytes);

        Handshake clientHello = new(HandshakeStage.CLIENT_HELLO, clientKey.PublicKey, clientNonce);

        Handshake serverHello = await session.RequestAsync<Handshake>(
            clientHello,
            options: RequestOptions.Default.WithTimeout(5000),
            predicate: p => p.Stage is HandshakeStage.SERVER_HELLO or HandshakeStage.ERROR,
            ct: ct).ConfigureAwait(false);

        if (serverHello.Stage == HandshakeStage.ERROR)
        {
            throw new NetworkException($"Handshake failed: {serverHello.Reason}");
        }

        if (!Handshake.IsValid(serverHello))
        {
            throw new NetworkException("Malformed Handshake SERVER_HELLO packet.");
        }

        Bytes32 sharedSecretEE = X25519.Agreement(clientKey.PrivateKey, serverHello.PublicKey);

        if (sharedSecretEE.IsZero)
        {
            throw new NetworkException("Handshake key agreement failed: Ephemeral shared secret is all zero.");
        }

        Bytes32 pinnedServerKey = string.IsNullOrEmpty(session.Options.ServerPublicKey) 
            ? Bytes32.Zero 
            : Bytes32.Parse(session.Options.ServerPublicKey);

        Bytes32 sharedSecretSE = pinnedServerKey.IsZero
            ? Bytes32.Zero 
            : X25519.Agreement(clientKey.PrivateKey, pinnedServerKey);

        if (!pinnedServerKey.IsZero && sharedSecretSE.IsZero)
        {
            throw new NetworkException("Handshake key agreement failed: Static shared secret is all zero.");
        }

        Bytes32 masterSecret = HandshakeX25519.ComputeMasterSecret(sharedSecretEE, sharedSecretSE);

        Bytes32 transcriptHash = Handshake.ComputeTranscriptHash(
            HandshakeX25519.ComposeTranscriptBuffer(clientKey.PublicKey, clientNonce, serverHello.PublicKey, serverHello.Nonce));

        Bytes32 expectedProof = HandshakeX25519.ComputeServerProof(masterSecret, transcriptHash);
        if (serverHello.Proof != expectedProof)
        {
            throw new NetworkException("Handshake SERVER_HELLO proof is invalid. Possible Man-in-the-Middle attack.");
        }

        Bytes32 sessionKey = HandshakeX25519.DeriveSessionKey(masterSecret, clientNonce, serverHello.Nonce, transcriptHash);

        Handshake clientFinish = new(HandshakeStage.CLIENT_FINISH, Bytes32.Zero, Bytes32.Zero, HandshakeX25519.ComputeClientProof(masterSecret, transcriptHash))
        {
            TranscriptHash = transcriptHash
        };

        Handshake serverFinish = await session.RequestAsync<Handshake>(
            clientFinish,
            options: RequestOptions.Default.WithTimeout(5000),
            predicate: p => p.Stage is HandshakeStage.SERVER_FINISH or HandshakeStage.ERROR,
            ct: ct).ConfigureAwait(false);

        if (serverFinish.Stage == HandshakeStage.ERROR)
        {
            throw new NetworkException($"Handshake failed during finish: {serverFinish.Reason}");
        }

        Bytes32 expectedFinish = HandshakeX25519.ComputeServerFinishProof(masterSecret, transcriptHash);
        if (serverFinish.Proof != expectedFinish)
        {
            throw new NetworkException("Handshake SERVER_FINISH proof is invalid.");
        }

        // Apply established connection settings
        session.Options.Secret = sessionKey;
        session.Options.Algorithm = CipherSuiteType.Chacha20Poly1305;
        session.Options.EncryptionEnabled = true;
        session.Options.SessionToken = serverFinish.SessionToken;
    }
}
