// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Codec.ProtocolFrames;
using Nalix.Codec.Security;
using Nalix.Codec.Security.Asymmetric;
using Nalix.Environment.Configuration;
using Nalix.Environment.Random;
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
    /// This method performs an authenticated Elliptic Curve Diffie-Hellman (ECDH) handshake using Curve25519.
    /// It generates an ephemeral key pair, exchanges public keys with the server, verifies server proofs 
    /// against a pinned <see cref="TransportOptions.ServerPublicKey"/>, and derives a shared session key.
    /// <para>
    /// Anonymous handshakes are forbidden for security reasons. The client MUST provide the expected 
    /// server public key in the session options to prevent Man-in-the-Middle (MitM) attacks.
    /// </para>
    /// Upon a successful handshake, the session's encryption settings (<see cref="SessionState.Secret"/>, 
    /// <see cref="SessionState.EncryptionEnabled"/>, etc.) are permanently updated to enforce AEAD 
    /// encryption for all subsequent outbound and inbound packets.
    /// </remarks>
    /// <param name="session">The connected transport session to perform the handshake on.</param>
    /// <param name="ct">A cancellation token that can be used to abort the handshake process.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="session"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the session is not connected.</exception>
    /// <exception cref="NetworkException">Thrown if the handshake fails due to malformed packets, invalid proofs, or key agreement failures.</exception>
    public static async ValueTask HandshakeAsync(this TransportSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsConnected)
        {
            throw new InvalidOperationException("Session must be connected to perform handshake.");
        }

        X25519.X25519KeyPair clientKey = X25519.GenerateKeyPair();

        Span<byte> clientNonceBytes = stackalloc byte[Bytes32.Size];
        Csprng.Fill(clientNonceBytes);
        Bytes32 clientNonce = new(clientNonceBytes);

        using SessionInit clientHello = new();
        clientHello.Initialize(clientKey.PublicKey, clientNonce);

        using SessionChallenge serverHello = await session.RequestAsync<SessionChallenge>(
            clientHello,
            options: RequestOptions.Default.WithTimeout(session.Options.ConnectTimeoutMillis),
            predicate: null,
            ct: ct).ConfigureAwait(false);

        if (!serverHello.Validate(out string? reason))
        {
            throw new NetworkException($"Malformed SessionChallenge packet: {reason}");
        }

        Bytes32 sharedSecretEE = X25519.Agreement(clientKey.PrivateKey, serverHello.PublicKey);

        if (sharedSecretEE.IsZero)
        {
            throw new NetworkException("Handshake key agreement failed: Ephemeral shared secret is all zero.");
        }

        if (string.IsNullOrEmpty(session.Options.ServerPublicKey))
        {
            using Control request = new();
            request.Initialize(ControlType.PUBLIC_KEY_REQUEST, flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE);

            using SessionTofu keyResponse = await session.RequestAsync<SessionTofu>(
                request,
                options: RequestOptions.Default.WithTimeout(5000),
                predicate: null,
                ct: ct).ConfigureAwait(false);

            if (!keyResponse.Validate(out string? keyReason))
            {
                throw new NetworkException($"Malformed SessionTofu response: {keyReason}");
            }

            string fetchedKeyHex = keyResponse.PublicKey.ToString();

            session.Options.ServerPublicKey = fetchedKeyHex;
            ConfigurationManager.Instance.UpdateValue<TransportOptions>(nameof(TransportOptions.ServerPublicKey), fetchedKeyHex);
            ConfigurationManager.Instance.Flush();
        }

        Bytes32 pinnedServerKey = Bytes32.Parse(session.Options.ServerPublicKey);

        Bytes32 sharedSecretSE = X25519.Agreement(clientKey.PrivateKey, pinnedServerKey);

        if (sharedSecretSE.IsZero)
        {
            throw new NetworkException("Handshake key agreement failed: Static shared secret is all zero.");
        }

        Bytes32 masterSecret = HandshakeX25519.ComputeMasterSecret(sharedSecretEE, sharedSecretSE);

        Bytes32 transcriptHash = HandshakeX25519.ComputeTranscriptHash(
            clientKey.PublicKey,
            clientNonce,
            serverHello.PublicKey,
            serverHello.Nonce);

        Bytes32 expectedProof = HandshakeX25519.ComputeServerProof(masterSecret, transcriptHash);
        if (serverHello.Proof != expectedProof)
        {
            throw new NetworkException("SessionChallenge proof is invalid. Possible Man-in-the-Middle attack.");
        }

        Bytes32 sessionKey = HandshakeX25519.DeriveSessionKey(masterSecret, clientNonce, serverHello.Nonce, transcriptHash);

        // BUG-FIX: Apply established connection settings BEFORE receiving SERVER_FINISH.
        // The server applies encryption immediately after receiving CLIENT_FINISH, 
        // meaning SERVER_FINISH will be ENCRYPTED. 
        // If we don't set the secret/algorithm here, the background reader will fail to decrypt it.
        session.State.Secret = sessionKey;
        session.Options.Algorithm = CipherSuiteType.Chacha20Poly1305;

        using SessionProof clientFinish = new();
        clientFinish.Initialize(HandshakeX25519.ComputeClientProof(masterSecret, transcriptHash));

        // Note: RequestAsync uses encrypt: false by default for the request itself, 
        // which is correct as the server expects SESSION_PROOF as PLAIN.
        try
        {
            using SessionEstablished serverFinish = await session.RequestAsync<SessionEstablished>(
                clientFinish,
                options: RequestOptions.Default.WithTimeout(5000),
                predicate: null,
                ct: ct).ConfigureAwait(false);

            if (!serverFinish.Validate(out string? finishReason))
            {
                throw new NetworkException($"Malformed SessionEstablished packet: {finishReason}");
            }

            Bytes32 expectedFinish = HandshakeX25519.ComputeServerFinishProof(masterSecret, transcriptHash);
            if (serverFinish.Proof != expectedFinish)
            {
                throw new NetworkException("SessionEstablished proof is invalid.");
            }

            // Finalize state
            session.State.EncryptionEnabled = true;
            session.State.SessionToken = serverFinish.SessionToken;
        }
        catch
        {
            session.State.Secret = Bytes32.Zero;
            session.State.EncryptionEnabled = false;
            session.Options.Algorithm = CipherSuiteType.Chacha20Poly1305;
            throw;
        }
    }
}
