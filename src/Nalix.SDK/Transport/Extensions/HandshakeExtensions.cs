// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Security.Asymmetric;
using Nalix.Framework.Security.Hashing;

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
[SkipLocalsInit]
public static class HandshakeExtensions
{
    /// <summary>Length of X25519 public keys in bytes.</summary>
    public const int PublicKeyLength = 32;

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
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task<bool> HandshakeAsync(
        this IClientConnection client,
        ushort opCode = 1,
        int timeoutMs = -1,
        Func<byte[], bool>? validateServerPublicKey = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (!client.IsConnected)
        {
            return false;
        }

        // Skip if session key is already installed.
        if (client.Options.Secret != null)
        {
            TcpSession.Logging?.Debug("[SDK.HandshakeAsync] Session key already installed; skipping.");
            return true;
        }

        IPacketRegistry catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
            ?? throw new InvalidOperationException("IPacketRegistry instance not found in InstanceManager.");

        TaskCompletionSource<Handshake> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        int effectiveTimeout = timeoutMs > 0 ? timeoutMs : client.Options.ConnectTimeoutMillis;
        linked.CancelAfter(effectiveTimeout);

        // Generate ephemeral key pair.
        X25519.X25519KeyPair kp = X25519.GenerateKeyPair();

        // Subscribe BEFORE sending to avoid a race where the server responds before we listen.
        using IDisposable sub = client.SubscribeTemp(OnMessageReceived, OnDisconnected);

        try
        {
            _ = await client.SendAsync(new Handshake(opCode, kp.PublicKey, ProtocolType.TCP), linked.Token)
                        .ConfigureAwait(false);

            TcpSession.Logging?.Debug("[SDK.HandshakeAsync] Handshake request sent.");

            using (tcs.LinkCancellation(linked.Token))
            {
                Handshake hs = await tcs.Task.ConfigureAwait(false);

                // Validate key length.
                if (hs.Data is not { Length: PublicKeyLength })
                {
                    TcpSession.Logging?.Warn("[SDK.HandshakeAsync] Server public key has unexpected length.");
                    return false;
                }

                // Optional pinning check.
                if (validateServerPublicKey is not null && !validateServerPublicKey(hs.Data))
                {
                    TcpSession.Logging?.Warn("[SDK.HandshakeAsync] Server public key rejected by validator.");
                    return false;
                }

                byte[]? secret = null;
                try
                {
                    secret = X25519.Agreement(kp.PrivateKey, hs.Data);
                    client.Options.Secret = Keccak256.HashData(secret);

                    TcpSession.Logging?.Info("[SDK.HandshakeAsync] Completed. Secret installed.");
                    return true;
                }
                finally
                {
                    // Zero sensitive material immediately, regardless of success or failure.
                    if (kp.PrivateKey is { Length: > 0 })
                    {
                        Array.Clear(kp.PrivateKey, 0, kp.PrivateKey.Length);
                    }

                    if (secret is { Length: > 0 })
                    {
                        Array.Clear(secret, 0, secret.Length);
                    }
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            TcpSession.Logging?.Debug($"[SDK.HandshakeAsync] Canceled: {oce.Message}.");
            return false;
        }
        catch (Exception ex)
        {
            TcpSession.Logging?.Error($"[SDK.HandshakeAsync] Failed: {ex}.");
            return false;
        }

        void OnMessageReceived(object? _, IBufferLease buffer)
        {
            // Always dispose the lease; deserialize takes a ReadOnlySpan copy.
            using (buffer)
            {
                if (!catalog.TryDeserialize(buffer.Span, out IPacket? p))
                {
                    return;
                }

                if (p is Handshake hs &&
                    hs.OpCode == opCode &&
                    hs.Protocol == ProtocolType.TCP)
                {
                    _ = tcs.TrySetResult(hs);
                }
            }
        }

        void OnDisconnected(object? _, Exception ex) => tcs.TrySetException(ex ?? new InvalidOperationException("Disconnected during handshake."));
    }
}
