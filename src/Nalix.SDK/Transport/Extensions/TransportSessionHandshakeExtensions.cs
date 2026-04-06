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
using Nalix.Framework.Security.Asymmetric;
using Nalix.Framework.Security.Primitives;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides extension methods for performing cryptographic handshakes over a TransportSession.
/// </summary>
public static class TransportSessionHandshakeExtensions
{
    /// <summary>
    /// Performs the default X25519 client-side handshake synchronously over the connected <see cref="TransportSession"/>.
    /// Derives the shared session key and automatically enables encryption for the session.
    /// </summary>
    /// <param name="session">The established transport session.</param>
    /// <param name="opCode">The opcode used to route handshake packets.</param>
    /// <param name="ct">The cancellation token.</param>
    public static async Task HandshakeAsync(this TransportSession session, ushort opCode = 0, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsConnected)
        {
            throw new InvalidOperationException("Session must be connected to perform handshake.");
        }

        X25519.X25519KeyPair clientKey = X25519.GenerateKeyPair();
        byte[] clientNonce = Csprng.GetBytes(Handshake.DynamicSize);

        Handshake clientHello = new(opCode, HandshakeStage.CLIENT_HELLO, clientKey.PublicKey, clientNonce);

        TaskCompletionSource<Handshake> serverHelloTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Handshake> serverFinishTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMessage(object? sender, IBufferLease lease)
        {
            try
            {
                // We attempt to deserialize all incoming packets during the handshake phase
                Handshake packet = Handshake.Deserialize(lease.Span);

                if (packet.OpCode != opCode)
                {
                    return;
                }

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

            if (!HandshakeCrypto.IsValid(serverHello))
            {
                throw new NetworkException("Malformed Handshake SERVER_HELLO packet.");
            }

            byte[] sharedSecret = X25519.Agreement(clientKey.PrivateKey, serverHello.PublicKey);
            if (HandshakeCrypto.IsAllZero(sharedSecret))
            {
                throw new NetworkException("Handshake key agreement failed: Shared secret is all zero.");
            }

            byte[] transcriptHash = Handshake.ComputeTranscriptHash(
                HandshakeCrypto.ComposeTranscriptBuffer(clientKey.PublicKey, clientNonce, serverHello.PublicKey, serverHello.Nonce));

            byte[] expectedProof = HandshakeCrypto.ComputeServerProof(sharedSecret, transcriptHash);
            if (!BitwiseOperations.FixedTimeEquals(serverHello.Proof, expectedProof))
            {
                throw new NetworkException("Handshake SERVER_HELLO proof is invalid.");
            }

            byte[] sessionKey = HandshakeCrypto.DeriveSessionKey(sharedSecret, clientNonce, serverHello.Nonce, transcriptHash);

            Handshake clientFinish = new(opCode, HandshakeStage.CLIENT_FINISH, [], [], HandshakeCrypto.ComputeClientProof(sharedSecret, transcriptHash))
            {
                TranscriptHash = transcriptHash
            };

            await session.SendAsync(clientFinish, ct).ConfigureAwait(false);

            using CancellationTokenRegistration reg2 = ct.Register(() => serverFinishTcs.TrySetCanceled());
            Handshake serverFinish = await serverFinishTcs.Task.ConfigureAwait(false);

            byte[] expectedFinish = HandshakeCrypto.ComputeServerFinishProof(sharedSecret, transcriptHash);
            if (!BitwiseOperations.FixedTimeEquals(serverFinish.Proof, expectedFinish))
            {
                throw new NetworkException("Handshake SERVER_FINISH proof is invalid.");
            }

            // Apply established connection settings
            session.Options.Secret = sessionKey;
            session.Options.Algorithm = CipherSuiteType.Chacha20Poly1305;
            session.Options.EncryptionEnabled = true;
        }
        finally
        {
            session.OnMessageReceived -= OnMessage;
        }
    }
}
