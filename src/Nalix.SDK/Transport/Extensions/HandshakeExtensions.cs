// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Infrastructure.Client;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.Framework.Injection;
using Nalix.Shared.Extensions;
using Nalix.Shared.Messaging.Controls;            // Handshake
using Nalix.Shared.Security.Asymmetric;
using Nalix.Shared.Security.Hashing;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Client-side cryptographic handshake for ReliableClient (event-driven).
/// Flow:
/// 1) Generate X25519 keypair.
/// 2) SEND Handshake(opCode, clientPublicKey, ProtocolType.TCP).
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
    /// - SEND Handshake(opCode, clientPublicKey, TCP)
    /// - Await server Handshake with 32-byte public key
    /// - Derive shared secret, install 32-byte session key (SHA3-256)
    /// Auto-unsubscribes the temporary listener and aborts on disconnect/timeout.
    /// </summary>
    /// <param name="client">RELIABLE client.</param>
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
        this IClient client,
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

        if (client.Options.EncryptionKey != null)
        {
            return true;
        }

        // Prepare TCS and timeout
        System.Threading.Tasks.TaskCompletionSource<Handshake> tcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        using System.Threading.CancellationTokenSource linked = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);

        // Generate ephemeral keypair
        X25519.X25519KeyPair kp = X25519.GenerateKeyPair();

        // Temporary listener (auto-removed in finally)
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OnMessageReceived(System.Object _, IBufferLease buffer)
        {
            InstanceManager.Instance.GetExistingInstance<IPacketCatalog>().TryDeserialize(buffer.Span, out IPacket p);

            if (p is Handshake hs &&
                hs.OpCode == opCode &&
                hs.Protocol == ProtocolType.TCP)
            {
                _ = tcs.TrySetResult(hs);
            }
        }

        // Abort on disconnect
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OnDisconnected(System.Object _, System.Exception ex) => _ = tcs.TrySetException(ex ?? new System.InvalidOperationException("Disconnected during handshake."));

        using System.IDisposable sub = client.SubscribeTemp(OnMessageReceived, OnDisconnected);

        try
        {
            // SEND client hello AFTER subscribing to avoid race
            await client.SendAsync(new Handshake(opCode, kp.PublicKey, ProtocolType.TCP), linked.Token)
                        .ConfigureAwait(false);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("[SDK.HandshakeAsync] Handshake request sent.");

            // Await server response (with timeout/ct)
            using (tcs.LinkCancellation(linked.Token))
            {
                Handshake hs = await tcs.Task.ConfigureAwait(false);

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
                                            .Info("[SDK.HandshakeAsync] Completed. EncryptionKey installed.");

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
        catch (System.OperationCanceledException oce)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[SDK.HandshakeAsync] Canceled: {oce.Message}.");
            return false;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.HandshakeAsync] Failed: {ex}.");
            return false;
        }
    }
}
