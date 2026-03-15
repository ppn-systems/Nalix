// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.Pooling;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Environment;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Asymmetric;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for the default server-side X25519 handshake protocol.
/// </summary>
[PacketController("Handshake")]
public sealed class HandshakeHandlers
{
    private static IConnectionHub? Hub => InstanceManager.Instance.GetExistingInstance<IConnectionHub>();

    private static readonly Bytes32 s_certificate = Bytes32.Zero;

    /// <inheritdoc/>
    static HandshakeHandlers()
    {
        string certPath = Path.Combine(Directories.ConfigurationDirectory, "certificate.private");

        if (!File.Exists(certPath))
        {
            throw new InternalErrorException(
                $"Handshake failed: certificate file 'certificate.private' was not found in '{Directories.ConfigurationDirectory}'. "
                + "Please provide a valid server identity file.");
        }

        try
        {
            string? hex = null;
            string[] lines = File.ReadAllLines(certPath);

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string trimmed = line.Trim();
                if (trimmed.StartsWith('#'))
                {
                    continue;
                }

                hex = trimmed;
                break;
            }

            if (string.IsNullOrWhiteSpace(hex))
            {
                throw new InternalErrorException(
                    $"Handshake failed: No valid certificate data found in '{certPath}'. Please check file format and content.");
            }

            s_certificate = Bytes32.Parse(hex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InternalErrorException(
                $"Handshake failed: Access denied while reading server identity from '{certPath}'. Exception detail: " + ex.Message, ex);
        }
        catch (IOException ex)
        {
            throw new InternalErrorException(
                $"Handshake failed: Unable to read server identity from '{certPath}'. Exception detail: " + ex.Message, ex);
        }
        catch (FormatException ex)
        {
            throw new InternalErrorException(
                $"Handshake failed: Invalid server identity format in '{certPath}'. Exception detail: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Handles incoming handshake signal packets.
    /// </summary>
    /// <param name="context">The packet context containing the handshake metadata.</param>
    /// <returns>A responding handshake packet or null if rejected/disconnected.</returns>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode((ushort)ProtocolOpCode.HANDSHAKE)]
    public static async ValueTask HandleAsync(IPacketContext<Handshake> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Handshake packet = context.Packet;
        IConnection connection = context.Connection;

        if (connection.Attributes.ContainsKey(ConnectionAttributes.HandshakeEstablished))
        {
            await RejectHandshakeAsync(connection, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        switch (packet.Stage)
        {
            case HandshakeStage.CLIENT_HELLO:
                await HandleClientHelloAsync(connection, packet).ConfigureAwait(false);
                break;

            case HandshakeStage.CLIENT_FINISH:
                await HandleClientFinishAsync(connection, packet).ConfigureAwait(false);
                break;

            case HandshakeStage.ERROR:
                connection.Disconnect("Handshake error received from peer.");
                break;

            case HandshakeStage.NONE:
            case HandshakeStage.SERVER_HELLO:
            case HandshakeStage.SERVER_FINISH:
            default:
                await RejectHandshakeAsync(connection, ProtocolReason.UNEXPECTED_MESSAGE).ConfigureAwait(false);
                break;
        }
    }

    #region Private Methods

    private static async ValueTask HandleClientHelloAsync(IConnection connection, Handshake packet)
    {
        // BUG-75: Prevent re-entry during active handshake to mitigate CPU DoS
        if (connection.Attributes.ContainsKey(ConnectionAttributes.HandshakeState))
        {
            await RejectHandshakeAsync(connection, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        Bytes32 sharedSecretEE = X25519.Agreement(serverKey.PrivateKey, packet.PublicKey);

        if (sharedSecretEE.IsZero)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        Bytes32 sharedSecretSE = X25519.Agreement(s_certificate, packet.PublicKey);

        if (sharedSecretSE.IsZero)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        Bytes32 masterSecret = HandshakeX25519.ComputeMasterSecret(sharedSecretEE, sharedSecretSE);

        Bytes32 serverNonce = new(Csprng.GetBytes(Bytes32.Size));

        Bytes32 transcriptHash = Handshake.ComputeTranscriptHash(
            HandshakeX25519.ComposeTranscriptBuffer(
                packet.PublicKey,
                packet.Nonce,
                serverKey.PublicKey,
                serverNonce));

        HandshakeContext state = new()
        {
            ClientPublicKey = packet.PublicKey,
            ClientNonce = packet.Nonce,
            SharedSecret = masterSecret,
            ServerNonce = serverNonce,
            ServerPublicKey = serverKey.PublicKey,
            TranscriptHash = transcriptHash,
            SessionKey = HandshakeX25519.DeriveSessionKey(masterSecret, packet.Nonce, serverNonce, transcriptHash)
        };

        connection.Attributes[ConnectionAttributes.HandshakeState] = state;

        using PacketLease<Handshake> lease = PacketPool<Handshake>.Rent();
        Handshake reply = lease.Value;
        reply.Stage = HandshakeStage.SERVER_HELLO;
        reply.PublicKey = serverKey.PublicKey;
        reply.Nonce = serverNonce;
        reply.Proof = HandshakeX25519.ComputeServerProof(masterSecret, transcriptHash);
        reply.Flags = (reply.Flags & ~PacketFlags.RELIABLE) | (packet.Flags & PacketFlags.RELIABLE);
        reply.TranscriptHash = transcriptHash;

        await connection.TCP.SendAsync(reply).ConfigureAwait(false);
    }

    private static async ValueTask HandleClientFinishAsync(IConnection connection, Handshake packet)
    {
        if (!TryGetState(connection, out HandshakeContext? state) || state is null)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        Bytes32 expectedProof = HandshakeX25519.ComputeClientProof(state.SharedSecret, state.TranscriptHash);
        if (packet.Proof != expectedProof)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.SIGNATURE_INVALID).ConfigureAwait(false);
            return;
        }

        if (packet.TranscriptHash != state.TranscriptHash)
        {
            await RejectHandshakeAsync(connection, ProtocolReason.CHECKSUM_FAILED).ConfigureAwait(false);
            return;
        }

        connection.Secret = state.SessionKey;
        connection.Algorithm = CipherSuiteType.Chacha20Poly1305;

        connection.Attributes[ConnectionAttributes.HandshakeEstablished] = true;
        _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);

        SessionEntry? session = Hub?.SessionStore.CreateSession(connection);
        if (session is not null)
        {
            await Hub!.SessionStore.StoreAsync(session).ConfigureAwait(false);
        }

        using PacketLease<Handshake> lease = PacketPool<Handshake>.Rent();
        Handshake reply = lease.Value;
        reply.Stage = HandshakeStage.SERVER_FINISH;
        reply.PublicKey = Bytes32.Zero;
        reply.Nonce = Bytes32.Zero;
        reply.Proof = HandshakeX25519.ComputeServerFinishProof(state.SharedSecret, state.TranscriptHash);
        reply.Flags = (reply.Flags & ~PacketFlags.RELIABLE) | (packet.Flags & PacketFlags.RELIABLE);
        reply.TranscriptHash = state.TranscriptHash;
        reply.SessionToken = session is not null ? Snowflake.NewId(session.Snapshot.SessionToken) : (Snowflake)connection.ID;

        await connection.TCP.SendAsync(reply).ConfigureAwait(false);
    }

    private static async ValueTask RejectHandshakeAsync(IConnection connection, ProtocolReason reason)
    {
        if (TryGetState(connection, out HandshakeContext? state) && state is not null)
        {
            _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);
        }

        try
        {
            using PacketLease<Handshake> lease = PacketPool<Handshake>.Rent();

            Handshake error = lease.Value;
            error.InitializeError(reason, flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE);
            await connection.TCP.SendAsync(error).ConfigureAwait(false);
        }
        finally
        {
            connection.Disconnect(reason.ToString());
        }
    }

    private static bool TryGetState(IConnection connection, [NotNullWhen(true)] out HandshakeContext? state)
    {
        if (connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeState, out object? boxed) &&
            boxed is HandshakeContext typed)
        {
            state = typed;
            return true;
        }

        state = null;
        return false;
    }

    private sealed record HandshakeContext
    {
        public Bytes32 ClientPublicKey { get; init; }
        public Bytes32 ClientNonce { get; init; }
        public Bytes32 ServerPublicKey { get; init; }
        public Bytes32 ServerNonce { get; init; }
        public Bytes32 SharedSecret { get; init; }
        public Bytes32 TranscriptHash { get; init; }
        public Bytes32 SessionKey { get; init; }
    }

    #endregion Private Methods
}
