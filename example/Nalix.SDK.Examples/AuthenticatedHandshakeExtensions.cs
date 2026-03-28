// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Transport;
using Nalix.Examples.Asymmetric;
using Nalix.Framework.Injection;
using Nalix.Framework.Security.Hashing;
using Nalix.SDK.Transport.Extensions;
using HandshakePacket = Nalix.Framework.DataFrames.SignalFrames.Handshake;

namespace Nalix.SDK.Examples;

/// <summary>
/// Client-side authenticated handshake helpers for <see cref="IClientConnection"/>.
/// The method below is intentionally explicit so users can see each security step.
/// </summary>
public static class AuthenticatedHandshakeExtensions
{
    /// <summary>
    /// Expected X25519 public-key length in bytes.
    /// </summary>
    public const int X25519PublicKeyLength = 32;

    /// <summary>
    /// Performs an authenticated X25519 handshake and stores the derived session secret on the client connection.
    /// </summary>
    public static async Task<bool> PerformAuthenticatedHandshakeAsync(
        this IClientConnection client,
        Func<string> clientIdentityProvider,
        Func<(byte[] PrivateKey, byte[] PublicKey)> ed25519KeyProvider,
        ushort opCode = 1,
        int timeoutMs = -1,
        Func<byte[], bool>? validateServerPublicKey = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(clientIdentityProvider);
        ArgumentNullException.ThrowIfNull(ed25519KeyProvider);

        if (!client.IsConnected || client.Options.Secret is not null)
        {
            // If the session already has a shared secret, there is nothing left to negotiate.
            return client.Options.Secret is not null;
        }

        // The registry is required to deserialize the response handshake packet.
        IPacketRegistry packetRegistry = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
            ?? throw new InvalidOperationException("An IPacketRegistry instance must be registered before starting the handshake.");

        // The TaskCompletionSource is completed when the server handshake arrives.
        TaskCompletionSource<HandshakePacket> responseSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Reuse the client timeout when the caller does not override it.
        int effectiveTimeout = timeoutMs > 0 ? timeoutMs : client.Options.ConnectTimeoutMillis;
        linkedCts.CancelAfter(effectiveTimeout);

        // Generate a one-time ephemeral key pair for this handshake only.
        X25519.X25519KeyPair ephemeralKeyPair = X25519.GenerateKeyPair();
        (byte[] PrivateKey, byte[] PublicKey) = ed25519KeyProvider();
        string identity = clientIdentityProvider();
        byte[] identityBytes = System.Text.Encoding.UTF8.GetBytes(identity);
        byte[] signedPayload = Ed25519.Combine(ephemeralKeyPair.PublicKey, identityBytes);
        byte[] signature = Ed25519.Sign(signedPayload, PrivateKey);

        // Build the handshake packet that the server will verify.
        HandshakePacket request = new(opCode, ephemeralKeyPair.PublicKey, ProtocolType.TCP)
        {
            Ed25519PublicKey = PublicKey,
            Ed25519Signature = signature,
            Identity = identity
        };

        // Subscribe first so the response cannot be missed if the server answers quickly.
        using IDisposable subscription = client.SubscribeTemp<HandshakePacket>(
            response => _ = responseSource.TrySetResult(response),
            ex => _ = responseSource.TrySetException(ex ?? new InvalidOperationException("Disconnected during handshake.")));

        try
        {
            await client.SendAsync(request, linkedCts.Token).ConfigureAwait(false);

            // Cancel the await if the caller cancels or the timeout expires.
            using CancellationTokenRegistration registration = linkedCts.Token.Register(() => _ = responseSource.TrySetCanceled(linkedCts.Token));

            HandshakePacket response = await responseSource.Task.ConfigureAwait(false);
            if (response.Data is not { Length: X25519PublicKeyLength })
            {
                // A valid response must contain exactly one X25519 public key.
                return false;
            }

            if (validateServerPublicKey is not null && !validateServerPublicKey(response.Data))
            {
                // Optional key pinning hook for applications that want stricter trust checks.
                return false;
            }

            byte[]? sharedSecret = null;
            try
            {
                // Derive the shared secret and transform it into the final session key.
                sharedSecret = X25519.Agreement(ephemeralKeyPair.PrivateKey, response.Data);
                client.Options.Secret = Keccak256.HashData(sharedSecret);
                return true;
            }
            finally
            {
                // Zero every sensitive buffer immediately after use.
                if (ephemeralKeyPair.PrivateKey is { Length: > 0 })
                {
                    Array.Clear(ephemeralKeyPair.PrivateKey, 0, ephemeralKeyPair.PrivateKey.Length);
                }

                if (sharedSecret is { Length: > 0 })
                {
                    Array.Clear(sharedSecret, 0, sharedSecret.Length);
                }

                if (PrivateKey is { Length: > 0 })
                {
                    Array.Clear(PrivateKey, 0, PrivateKey.Length);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Returning false keeps the example easy to consume from console apps.
            return false;
        }
        catch
        {
            // Any transport, parsing, or crypto error should fail the handshake cleanly.
            return false;
        }
    }
}
