// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Random;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Asymmetric;
using Nalix.Framework.Security.Primitives;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides extension methods for performing cryptographic handshakes over a <see cref="TransportSession"/>.
/// </summary>
public static class TcpSessionX25519Extensions
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
        byte[] clientNonce = Csprng.GetBytes(Handshake.DynamicSize);

        // OpCode ignore
        Handshake clientHello = new(HandshakeStage.CLIENT_HELLO, clientKey.PublicKey, clientNonce);

        TaskCompletionSource<Handshake> serverHelloTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Handshake> serverFinishTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMessage(object? sender, IBufferLease lease)
        {
            try
            {
                // We attempt to deserialize all incoming packets during the handshake phase
                Handshake packet = Handshake.Deserialize(lease.Span);

                switch (packet.Stage)
                {
                    case HandshakeStage.SERVER_HELLO:
                        _ = serverHelloTcs.TrySetResult(packet);
                        break;
                    case HandshakeStage.SERVER_FINISH:
                        _ = serverFinishTcs.TrySetResult(packet);
                        break;
                    case HandshakeStage.ERROR:
                        _ = serverHelloTcs.TrySetException(new NetworkException("Handshake error received from server."));
                        _ = serverFinishTcs.TrySetException(new NetworkException("Handshake error received from server."));
                        break;
                    case HandshakeStage.NONE:
                    case HandshakeStage.CLIENT_HELLO:
                    case HandshakeStage.CLIENT_FINISH:
                    default:
                        NetworkException unexpectedEx = new($"Unexpected handshake stage received from server: {packet.Stage}");
                        _ = serverHelloTcs.TrySetException(unexpectedEx);
                        _ = serverFinishTcs.TrySetException(unexpectedEx);
                        break;
                }
            }
            catch
            {
                // Ignore parsing errors for packets that might not be handshakes or are malformed
            }
        }

        session.OnMessageReceived += OnMessage;

        try
        {
            await session.SendAsync(clientHello, ct).ConfigureAwait(false);

            using CancellationTokenRegistration reg1 = ct.Register(() => serverHelloTcs.TrySetCanceled());
            Handshake serverHello = await serverHelloTcs.Task.ConfigureAwait(false);

            if (!Handshake.IsValid(serverHello))
            {
                throw new NetworkException("Malformed Handshake SERVER_HELLO packet.");
            }

            byte[] sharedSecret = X25519.Agreement(clientKey.PrivateKey, serverHello.PublicKey);
            if (BitwiseOperations.IsZero(sharedSecret))
            {
                throw new NetworkException("Handshake key agreement failed: Shared secret is all zero.");
            }

            byte[] transcriptHash = Handshake.ComputeTranscriptHash(
                HandshakeX25519.ComposeTranscriptBuffer(clientKey.PublicKey, clientNonce, serverHello.PublicKey, serverHello.Nonce));

            byte[] expectedProof = HandshakeX25519.ComputeServerProof(sharedSecret, transcriptHash);
            if (!BitwiseOperations.FixedTimeEquals(serverHello.Proof, expectedProof))
            {
                throw new NetworkException("Handshake SERVER_HELLO proof is invalid.");
            }

            byte[] sessionKey = HandshakeX25519.DeriveSessionKey(sharedSecret, clientNonce, serverHello.Nonce, transcriptHash);

            Handshake clientFinish = new(HandshakeStage.CLIENT_FINISH, [], [], HandshakeX25519.ComputeClientProof(sharedSecret, transcriptHash))
            {
                TranscriptHash = transcriptHash
            };

            await session.SendAsync(clientFinish, ct).ConfigureAwait(false);

            using CancellationTokenRegistration reg2 = ct.Register(() => serverFinishTcs.TrySetCanceled());
            Handshake serverFinish = await serverFinishTcs.Task.ConfigureAwait(false);

            byte[] expectedFinish = HandshakeX25519.ComputeServerFinishProof(sharedSecret, transcriptHash);
            if (!BitwiseOperations.FixedTimeEquals(serverFinish.Proof, expectedFinish))
            {
                throw new NetworkException("Handshake SERVER_FINISH proof is invalid.");
            }

            // Apply established connection settings
            session.Options.Secret = sessionKey;
            session.Options.Algorithm = CipherSuiteType.Chacha20Poly1305;
            session.Options.EncryptionEnabled = true;
            session.Options.SessionToken = serverFinish.SessionToken;
        }
        finally
        {
            session.OnMessageReceived -= OnMessage;
        }
    }
}
