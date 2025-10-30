// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;                     // ProtocolType
using Nalix.Framework.Cryptography.Asymmetric;    // X25519
using Nalix.Framework.Cryptography.Hashing;       // SHA3256 (SHA3-256)
using Nalix.Framework.Injection;
using Nalix.Shared.Messaging.Controls;            // Handshake

namespace Nalix.SDK.Remote.Extensions;

/// <summary>
/// Client-side cryptographic handshake for ReliableClient (event-driven).
/// Flow:
/// 1) Generate X25519 keypair.
/// 2) Send Handshake(opCode, clientPublicKey, ProtocolType.TCP).
/// 3) Await server Handshake containing 32-byte server public key.
/// 4) Derive shared secret and install a 32-byte session key via SHA3-256(secret).
/// </summary>
public static class HandshakeExtensions
{
    private const System.Int32 PublicKeyLen = 32;

    /// <summary>
    /// Initiates the handshake by sending client public key.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">Client not connected or key already installed.</exception>
    public static System.Threading.Tasks.Task<X25519.X25519KeyPair> InitiateHandshakeAsync(
        this ReliableClient client,
        System.UInt16 opCode,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        if (!client.IsConnected)
        {
            throw new System.InvalidOperationException("Client not connected.");
        }

        if (client.Options.EncryptionKey is { Length: PublicKeyLen })
        {
            throw new System.InvalidOperationException(
                "Handshake already completed: encryption key is installed.");
        }

        X25519.X25519KeyPair keyPair = X25519.GenerateKeyPair();

        return SendAndReturnAsync(client, keyPair, opCode, ct);

        static async System.Threading.Tasks.Task<X25519.X25519KeyPair> SendAndReturnAsync(
            ReliableClient c,
            X25519.X25519KeyPair kp,
            System.UInt16 code,
            System.Threading.CancellationToken token)
        {
            await c.SendAsync(new Handshake(code, kp.PublicKey, ProtocolType.TCP), token)
                   .ConfigureAwait(false);

            InstanceManager.Instance.GetExistingInstance<ILogger>()
                ?.Debug("Handshake request sent (client public key).");

            return kp;
        }
    }

    /// <summary>
    /// Completes the handshake using a received <see cref="Handshake"/> packet (server public key).
    /// </summary>
    /// <returns>true if key installed; otherwise false.</returns>
    public static System.Boolean FinishHandshake(
        this ReliableClient client,
        X25519.X25519KeyPair clientKeyPair,
        IPacket packet)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        if (clientKeyPair.PrivateKey is null)
        {
            throw new System.ArgumentNullException(nameof(clientKeyPair),
                "X25519 private key is required.");
        }

        if (packet is not Handshake hs || hs.Data is not { Length: PublicKeyLen })
        {
            return false;
        }

        System.Byte[] secret = null;

        try
        {
            // X25519 shared secret (clientPriv, serverPub)
            secret = X25519.Agreement(clientKeyPair.PrivateKey, hs.Data);

            // Derive 32-byte session key using SHA3-256(secret)
            client.Options.EncryptionKey = SHA3256.HashData(secret);

            InstanceManager.Instance.GetExistingInstance<ILogger>()
                ?.Info("Handshake completed. EncryptionKey installed.");

            return true;
        }
        finally
        {
            if (clientKeyPair.PrivateKey is not null)
            {
                System.Array.Clear(clientKeyPair.PrivateKey, 0, clientKeyPair.PrivateKey.Length);
            }

            if (secret is not null)
            {
                System.Array.Clear(secret, 0, secret.Length);
            }
        }
    }

    /// <summary>
    /// One-shot helper: send client key, then await matching server Handshake and install the session key.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="opCode">Operation code to match on both request and response.</param>
    /// <param name="timeoutMs">Total timeout (send + await response).</param>
    /// <param name="ct"></param>
    /// <exception cref="System.TimeoutException">If no matching response arrives in time.</exception>
    /// <exception cref="System.InvalidOperationException">If not connected or key already installed.</exception>
    public static async System.Threading.Tasks.Task<System.Boolean> WaitAndFinishHandshakeAsync(
        this ReliableClient client,
        System.UInt16 opCode,
        System.Int32 timeoutMs = 5000,
        System.Threading.CancellationToken ct = default)
    {
        // 1) Send client pubkey
        X25519.X25519KeyPair keyPair = await client
            .InitiateHandshakeAsync(opCode, ct)
            .ConfigureAwait(false);

        // 2) Await server Handshake that matches opCode (and optionally protocol TCP)
        Handshake serverHs = await client.AwaitPacketAsync<Handshake>(
            predicate: hs => hs.OpCode == opCode /* && hs.Protocol == ProtocolType.TCP */,
            timeoutMs: timeoutMs,
            ct: ct
        ).ConfigureAwait(false);

        // 3) Derive & install key
        return client.FinishHandshake(keyPair, serverHs);
    }
}
