// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Examples.Asymmetric;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Security.Hashing;
using Nalix.Network.Connections;
using Nalix.Network.Examples.Attributes;
using Nalix.Network.Routing;
using HandshakePacket = Nalix.Framework.DataFrames.SignalFrames.Handshake;

namespace Nalix.Network.Examples.Handlers;

/// <summary>
/// Packet handlers used by the network example.
/// </summary>
[PacketController]
public sealed class PacketCommandHandler
{
    /// <summary>
    /// Minimal "smoke test" route.
    /// If this packet comes back unchanged, the routing pipeline is wired correctly.
    /// </summary>
    [PacketOpcode(0)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketTag("ping")]
    public static async Task Ping(PacketContext<IPacket> context)
        => await context.Sender.SendAsync(context.Packet).ConfigureAwait(false);

    /// <summary>
    /// Second smoke test route.
    /// Keeping both routes separate helps users see how opcode-to-method mapping works.
    /// </summary>
    [PacketOpcode(1)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketTag("pong")]
    public static async Task Pong(PacketContext<IPacket> context)
        => await context.Sender.SendAsync(context.Packet).ConfigureAwait(false);

    /// <summary>
    /// Performs the server side of the demo handshake.
    /// The flow is:
    /// 1. Validate the client payload.
    /// 2. Verify the Ed25519 signature.
    /// 3. Derive a shared secret from X25519.
    /// 4. Upgrade the connection permissions.
    /// </summary>
    [PacketOpcode(2)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketTag("handshake")]
    public static async Task Handshake(PacketContext<IPacket> context)
    {
        HandshakePacket handshake = (HandshakePacket)context.Packet;

        // Reject the packet early if any required field is missing.
        if (handshake.Data is null ||
            handshake.Ed25519PublicKey is null ||
            handshake.Ed25519Signature is null ||
            string.IsNullOrWhiteSpace(handshake.Identity))
        {
            await context.Connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.MISSING_REQUIRED_FIELD,
                ProtocolAdvice.FIX_AND_RETRY).ConfigureAwait(false);
            return;
        }

        // The handshake expects fixed-size public keys and signatures.
        if (handshake.Data.Length != 32 ||
            handshake.Ed25519PublicKey.Length != 32 ||
            handshake.Ed25519Signature.Length != 64)
        {
            await context.Connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.VALIDATION_FAILED,
                ProtocolAdvice.FIX_AND_RETRY).ConfigureAwait(false);
            return;
        }

        // The identity is included in the signed payload so it cannot be swapped later.
        byte[] identityBytes = System.Text.Encoding.UTF8.GetBytes(handshake.Identity);
        byte[] payloadToVerify = Ed25519.Combine(handshake.Data, identityBytes);
        if (!Ed25519.Verify(handshake.Ed25519Signature, payloadToVerify, handshake.Ed25519PublicKey))
        {
            await context.Connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.VALIDATION_FAILED,
                ProtocolAdvice.FIX_AND_RETRY).ConfigureAwait(false);
            return;
        }

        // Store identity information on the connection so later handlers can reuse it.
        context.Connection.Attributes.Add("Identity", handshake.Identity);
        context.Connection.Attributes.Add("Ed25519-PublicKey", handshake.Ed25519PublicKey);

        // Use the object pool so the example follows the same allocation pattern as the library.
        HandshakePacket response = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                           .Get<HandshakePacket>();
        byte[] payload = [];

        try
        {
            // Generate a server ephemeral key pair and derive the shared secret from the client key.
            X25519.X25519KeyPair keyPair = X25519.GenerateKeyPair();
            byte[] sharedSecret = X25519.Agreement(keyPair.PrivateKey, handshake.Data);

            // Hash the shared secret before storing it on the connection.
            context.Connection.Secret = Keccak256.HashData(sharedSecret);

            // Wipe sensitive data as soon as it is no longer needed.
            Array.Clear(keyPair.PrivateKey, 0, keyPair.PrivateKey.Length);
            Array.Clear(sharedSecret, 0, sharedSecret.Length);
            context.Connection.Level = PermissionLevel.GUEST;

            // Respond with the server public key so the client can finish key agreement.
            response.OpCode = 2;
            response.Data = keyPair.PublicKey;
            payload = response.Serialize();
        }
        catch
        {
            // If something fails, reset the state so the connection is not half-upgraded.
            context.Connection.Secret = null!;
            context.Connection.Level = PermissionLevel.NONE;

            await context.Connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.INTERNAL_ERROR,
                ProtocolAdvice.BACKOFF_RETRY,
                flags: ControlFlags.IS_TRANSIENT).ConfigureAwait(false);
        }
        finally
        {
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return(response);
        }

        if (payload is { Length: > 0 })
        {
            await context.Connection.TCP.SendAsync(payload).ConfigureAwait(false);
        }
    }
}
