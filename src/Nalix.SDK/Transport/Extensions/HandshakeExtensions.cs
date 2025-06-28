// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Injection;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Security.Asymmetric;
using Nalix.Shared.Security.Hashing;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Client-side cryptographic handshake extension for <see cref="IClientConnection"/> (event-driven).
/// </summary>
/// <remarks>
/// Flow:
/// <list type="number">
/// <item>Generate an ephemeral X25519 key pair.</item>
/// <item>SEND <see cref="Handshake"/> (opCode, clientPublicKey, <see cref="ProtocolType.TCP"/>).</item>
/// <item>Await server <see cref="Handshake"/> containing a 32-byte server public key.</item>
/// <item>Derive a shared secret and install a 32-byte session key via SHA3-256 (Keccak-256).</item>
/// </list>
/// Private key and shared secret bytes are zeroed immediately after use.
/// </remarks>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class HandshakeExtensions
{
    /// <summary>Length of X25519 public keys in bytes.</summary>
    public const System.Int32 PublicKeyLength = 32;

    // Lazy logger resolution: avoids hard startup failure if ILogger is registered after this type loads.
    private static ILogger Log => InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// Performs a full X25519 Diffie-Hellman handshake with the server.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="opCode">Operation code to correlate request/response. Default is <c>1</c>.</param>
    /// <param name="timeoutMs">Total timeout in milliseconds covering both send and await. Default is 5 000 ms.</param>
    /// <param name="validateServerPublicKey">
    /// Optional public-key pinning callback. Return <c>true</c> to accept the server key; <c>false</c> to reject.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the handshake succeeded (or an encryption key was already installed);
    /// <c>false</c> on timeout, rejection, or any failure.
    /// </returns>
    /// <remarks>
    /// The method subscribes to <see cref="IClientConnection.OnMessageReceived"/> and
    /// <see cref="IClientConnection.OnDisconnected"/> only for the duration of the handshake
    /// and automatically unsubscribes via <see cref="SubscriptionExtensions.SubscribeTemp"/>.
    /// </remarks>
    public static async System.Threading.Tasks.Task<System.Boolean> HandshakeAsync(
        this IClientConnection client,
        System.UInt16 opCode = 1,
        System.Int32 timeoutMs = 5_000,
        System.Func<System.Byte[], System.Boolean> validateServerPublicKey = null,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        if (!client.IsConnected)
        {
            return false;
        }

        // Skip if session key is already installed.
        if (client.Options.EncryptionKey != null)
        {
            Log?.Debug("[SDK.HandshakeAsync] Session key already installed; skipping.");
            return true;
        }

        IPacketCatalog catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>();

        System.Threading.Tasks.TaskCompletionSource<Handshake> tcs =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        using System.Threading.CancellationTokenSource linked =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);

        // Generate ephemeral key pair.
        X25519.X25519KeyPair kp = X25519.GenerateKeyPair();

        void OnMessageReceived(System.Object _, IBufferLease buffer)
        {
            // Always dispose the lease; deserialize takes a ReadOnlySpan copy.
            using (buffer)
            {
                if (!catalog.TryDeserialize(buffer.Span, out IPacket p))
                {
                    return;
                }

                if (p is Handshake hs &&
                    hs.OpCode == opCode &&
                    hs.Protocol == ProtocolType.TCP)
                {
                    tcs.TrySetResult(hs);
                }
            }
        }

        void OnDisconnected(System.Object _, System.Exception ex)
            => tcs.TrySetException(
                ex ?? new System.InvalidOperationException("Disconnected during handshake."));

        // Subscribe BEFORE sending to avoid a race where the server responds before we listen.
        using System.IDisposable sub = client.SubscribeTemp(OnMessageReceived, OnDisconnected);

        try
        {
            await client.SendAsync(new Handshake(opCode, kp.PublicKey, ProtocolType.TCP), linked.Token)
                        .ConfigureAwait(false);

            Log?.Debug("[SDK.HandshakeAsync] Handshake request sent.");

            using (tcs.LinkCancellation(linked.Token))
            {
                Handshake hs = await tcs.Task.ConfigureAwait(false);

                // Validate key length.
                if (hs.Data is not { Length: PublicKeyLength })
                {
                    Log?.Warn("[SDK.HandshakeAsync] Server public key has unexpected length.");
                    return false;
                }

                // Optional pinning check.
                if (validateServerPublicKey is not null && !validateServerPublicKey(hs.Data))
                {
                    Log?.Warn("[SDK.HandshakeAsync] Server public key rejected by validator.");
                    return false;
                }

                System.Byte[] secret = null;
                try
                {
                    secret = X25519.Agreement(kp.PrivateKey, hs.Data);
                    client.Options.EncryptionKey = Keccak256.HashData(secret);

                    Log?.Info("[SDK.HandshakeAsync] Completed. EncryptionKey installed.");
                    return true;
                }
                finally
                {
                    // Zero sensitive material immediately, regardless of success or failure.
                    if (kp.PrivateKey is { Length: > 0 })
                    {
                        System.Array.Clear(kp.PrivateKey, 0, kp.PrivateKey.Length);
                    }

                    if (secret is { Length: > 0 })
                    {
                        System.Array.Clear(secret, 0, secret.Length);
                    }
                }
            }
        }
        catch (System.OperationCanceledException oce)
        {
            Log?.Debug($"[SDK.HandshakeAsync] Canceled: {oce.Message}.");
            return false;
        }
        catch (System.Exception ex)
        {
            Log?.Error($"[SDK.HandshakeAsync] Failed: {ex}.");
            return false;
        }
    }
}