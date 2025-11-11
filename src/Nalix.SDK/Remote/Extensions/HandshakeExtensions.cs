// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;                     // ProtocolType
using Nalix.Framework.Injection;
using Nalix.Shared.Security.Asymmetric;
using Nalix.Shared.Security.Hashing;
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
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class HandshakeExtensions
{
    /// <summary>
    /// Length of X25519 public keys in bytes.
    /// </summary>
    public const System.Int32 PublicKeyLength = 32;

    /// <summary>
    /// Perform X25519 handshake end-to-end:
    /// - Generate ephemeral keypair
    /// - Send Handshake(opCode, clientPublicKey, TCP)
    /// - Await server Handshake with 32-byte public key
    /// - Derive shared secret, install 32-byte session key (SHA3-256)
    /// Auto-unsubscribes the temporary listener and aborts on disconnect/timeout.
    /// </summary>
    /// <param name="client">Reliable client.</param>
    /// <param name="opCode">Operation code to match request/response.</param>
    /// <param name="timeoutMs">Total timeout (send + await).</param>
    /// <param name="validateServerPublicKey">
    /// Optional pinning check: return true to accept the server's public key.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>true if handshake succeeded (or already done); false on timeout/failure.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static async System.Threading.Tasks.Task<System.Boolean> HandshakeAsync(
        this ReliableClient client,
        System.UInt16 opCode = 1,
        System.Int32 timeoutMs = 5_000,
        System.Func<System.Byte[], System.Boolean> validateServerPublicKey = null,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        if (!client.IsConnected)
        {
            return false; // not connected
        }

        // Idempotent: already handshaked
        if (client.IsHandshaked)
        {
            return true;
        }

        // Prepare TCS and timeout
        var tcs = new System.Threading.Tasks.TaskCompletionSource<Handshake>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        using var linked = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);

        // Generate ephemeral keypair
        var kp = X25519.GenerateKeyPair();

        // Temporary listener (auto-removed in finally)
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OnPacket(IPacket p)
        {
            if (p is Handshake hs && hs.OpCode == opCode /* && hs.Protocol == ProtocolType.TCP */)
            {
                tcs.TrySetResult(hs);
            }
        }

        // Abort on disconnect
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OnDisconnected(System.Exception ex)
        {
            tcs.TrySetException(ex ?? new System.InvalidOperationException("Disconnected during handshake."));
        }

        client.PacketReceived += OnPacket;
        client.Disconnected += OnDisconnected;

        try
        {
            // Send client hello AFTER subscribing to avoid race
            await client.SendAsync(new Handshake(opCode, kp.PublicKey, ProtocolType.TCP), linked.Token)
                        .ConfigureAwait(false);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("Handshake request sent (client public key).");

            // Await server response (with timeout/ct)
            using (linked.Token.Register(() => tcs.TrySetCanceled(linked.Token)))
            {
                var hs = await tcs.Task.ConfigureAwait(false);

                // Basic checks
                if (hs.Data is not { Length: PublicKeyLength })
                {
                    return false;
                }

                if (validateServerPublicKey is not null && !validateServerPublicKey(hs.Data))
                {
                    return false;
                }

                // Derive & install session key
                System.Byte[] secret = null;
                try
                {
                    secret = X25519.Agreement(kp.PrivateKey, hs.Data);
                    client.Options.EncryptionKey = Keccak256.HashData(secret);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Info("Handshake completed. EncryptionKey installed.");

                    return true;
                }
                finally
                {
                    if (kp.PrivateKey is not null)
                    {
                        System.Array.Clear(kp.PrivateKey, 0, kp.PrivateKey.Length);
                    }

                    if (secret is not null)
                    {
                        System.Array.Clear(secret, 0, secret.Length);
                    }
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            // Auto-unsubscribe
            client.PacketReceived -= OnPacket;
            client.Disconnected -= OnDisconnected;
        }
    }
}
