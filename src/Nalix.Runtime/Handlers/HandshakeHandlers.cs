// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Codec.Pooling;
using Nalix.Codec.ProtocolFrames;
using Nalix.Codec.Security;
using Nalix.Codec.Security.Asymmetric;
using Nalix.Codec.Security.Primitives;
using Nalix.Environment.IO;
using Nalix.Environment.Random;
using Nalix.Framework.Injection;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for the default server-side X25519 handshake protocol.
/// </summary>
[PacketController("Nalix.Handshake")]
public sealed class HandshakeHandlers
{
    #region APIs

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
        /*
         * [Handshake Entry Point]
         * We route the handshake packet based on its Stage.
         * The server only expects CLIENT_HELLO and CLIENT_FINISH.
         */
        ArgumentNullException.ThrowIfNull(context);

        Handshake packet = context.Packet;
        IConnection connection = context.Connection;

        if (connection.Attributes.ContainsKey(ConnectionAttributes.HandshakeEstablished))
        {
            await RejectHandshakeAsync(context, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        switch (packet.Stage)
        {
            case HandshakeStage.CLIENT_HELLO:
                await HandleClientHelloAsync(context).ConfigureAwait(false);
                break;

            case HandshakeStage.CLIENT_FINISH:
                await HandleClientFinishAsync(context).ConfigureAwait(false);
                break;

            case HandshakeStage.ERROR:
                connection.Disconnect("Handshake error received from peer.");
                break;

            case HandshakeStage.NONE:
            case HandshakeStage.SERVER_HELLO:
            case HandshakeStage.SERVER_FINISH:
            default:
                await RejectHandshakeAsync(context, ProtocolReason.UNEXPECTED_MESSAGE).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Initializes the handshake handlers with the default certificate.
    /// </summary>
    /// <remarks>
    /// This is called automatically by the host builder if no custom certificate path is specified.
    /// </remarks>
    public static void Initialize()
    {
        if (Volatile.Read(ref s_isInitialized) != 0)
        {
            return;
        }

        lock (s_initLock)
        {
            if (Volatile.Read(ref s_isInitialized) != 0)
            {
                return;
            }

            LOAD_CERTIFICATE(Path.Combine(Directories.ConfigurationDirectory, "certificate.private"));
            Volatile.Write(ref s_isInitialized, 1);
        }
    }

    /// <summary>
    /// Sets a custom path for the server identity certificate and initializes it.
    /// </summary>
    /// <param name="path">The absolute path to the certificate file.</param>
    public static void SetCertificatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (s_initLock)
        {
            LOAD_CERTIFICATE(path);
            Volatile.Write(ref s_isInitialized, 1);
        }
    }

    #endregion APIs

    #region Fields

    private static int s_isInitialized;
    private static readonly Lock s_initLock = new();
    private static Bytes32 s_certificate = Bytes32.Zero;
    private static Bytes32 s_serverPublicKey = Bytes32.Zero;

    /// <summary>
    /// Gets the server's public key (derived from the certificate).
    /// </summary>
    public static Bytes32 ServerPublicKey => s_serverPublicKey;

    #endregion Fields

    #region Private Methods

    #region Nested Types

    private sealed class HandshakeContext : IDisposable
    {
        public Bytes32 ClientPublicKey { get; init; }
        public Bytes32 ClientNonce { get; init; }
        public Bytes32 ServerPublicKey { get; init; }
        public Bytes32 ServerNonce { get; init; }
        public Bytes32 SharedSecret;
        public Bytes32 TranscriptHash { get; init; }
        public Bytes32 SessionKey;

        public void Dispose()
        {
            MemorySecurity.ZeroMemory(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref SessionKey, 1)));
            MemorySecurity.ZeroMemory(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref SharedSecret, 1)));
        }
    }

    #endregion Nested Types

    private static void LOAD_CERTIFICATE(string certPath)
    {
        if (!File.Exists(certPath))
        {
            CREATE_CERTIFICATE(certPath);
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
            s_serverPublicKey = X25519.GenerateKeyFromPrivateKey(s_certificate).PublicKey;
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

    private static void CREATE_CERTIFICATE(string certPath)
    {
        string? directory = Path.GetDirectoryName(certPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        X25519.X25519KeyPair key = X25519.GenerateKeyPair();
        if (!TRY_WRITE_NEW_FILE(certPath, key.PrivateKey.ToString()))
        {
            return;
        }

        string publicPath = Path.Combine(
            directory ?? string.Empty,
            Path.GetFileNameWithoutExtension(certPath) + ".public");

        _ = TRY_WRITE_NEW_FILE(publicPath, key.PublicKey.ToString());
    }

    private static bool TRY_WRITE_NEW_FILE(string path, string content)
    {
        try
        {
            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using StreamWriter writer = new(stream);
            writer.WriteLine(content);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }
    }

    private static async ValueTask HandleClientHelloAsync(IPacketContext<Handshake> context)
    {
        Handshake packet = context.Packet;
        IConnection connection = context.Connection;

        /*
         * [Stage 1: Client Hello]
         * 1. Acquire a handshake slot to prevent race conditions.
         * 2. Generate a fresh ephemeral X25519 key pair for the server.
         * 3. Perform two key agreements:
         *    - EE (Ephemeral-Ephemeral): For forward secrecy.
         *    - SE (Static-Ephemeral): For server authentication.
         * 4. Compute transcript hash and derive the temporary session key.
         */
        // BUG-75: Atomically reserve handshake state to prevent re-entry races.
        if (!TryAcquireHandshakeSlot(connection, out object claimToken))
        {
            await RejectHandshakeAsync(context, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        Bytes32 sharedSecretEE = X25519.Agreement(serverKey.PrivateKey, packet.PublicKey);

        if (sharedSecretEE.IsZero)
        {
            await RejectHandshakeAsync(context, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        Bytes32 sharedSecretSE = X25519.Agreement(s_certificate, packet.PublicKey);

        if (sharedSecretSE.IsZero)
        {
            await RejectHandshakeAsync(context, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        Bytes32 masterSecret = HandshakeX25519.ComputeMasterSecret(sharedSecretEE, sharedSecretSE);

        Bytes32 serverNonce = new(Csprng.GetBytes(Bytes32.Size));

        Bytes32 transcriptHash = HandshakeX25519.ComputeTranscriptHash(
            packet.PublicKey,
            packet.Nonce,
            serverKey.PublicKey,
            serverNonce);

#pragma warning disable CA2000 // Dispose objects before losing scope
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
#pragma warning restore CA2000 // Dispose objects before losing scope

        if (!TryPublishHandshakeState(connection, claimToken, state))
        {
            await RejectHandshakeAsync(context, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        using PacketScope<Handshake> lease = PacketFactory<Handshake>.Acquire();
        Handshake reply = lease.Value;
        reply.Stage = HandshakeStage.SERVER_HELLO;
        reply.PublicKey = serverKey.PublicKey;
        reply.Nonce = serverNonce;
        reply.Proof = HandshakeX25519.ComputeServerProof(masterSecret, transcriptHash);
        reply.Flags = (reply.Flags & ~PacketFlags.RELIABLE) | (packet.Flags & PacketFlags.RELIABLE);
        reply.TranscriptHash = transcriptHash;

        await context.Sender.SendAsync(reply).ConfigureAwait(false);
    }

    private static async ValueTask HandleClientFinishAsync(IPacketContext<Handshake> context)
    {
        Handshake packet = context.Packet;
        IConnection connection = context.Connection;

        if (!TryGetState(connection, out HandshakeContext? state) || state is null)
        {
            await RejectHandshakeAsync(context, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        Bytes32 expectedProof = HandshakeX25519.ComputeClientProof(state.SharedSecret, state.TranscriptHash);
        if (packet.Proof != expectedProof)
        {
            await RejectHandshakeAsync(context, ProtocolReason.SIGNATURE_INVALID).ConfigureAwait(false);
            return;
        }

        if (packet.TranscriptHash != state.TranscriptHash)
        {
            await RejectHandshakeAsync(context, ProtocolReason.CHECKSUM_FAILED).ConfigureAwait(false);
            return;
        }

        connection.Secret = state.SessionKey;
        connection.Algorithm = CipherSuiteType.Chacha20Poly1305;

        Bytes32 expectedFinish = HandshakeX25519.ComputeServerFinishProof(state.SharedSecret, state.TranscriptHash);

        connection.Attributes[ConnectionAttributes.HandshakeEstablished] = true;
        if (connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeState, out object? removedState) && removedState is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);

        if (InstanceManager.Instance.GetExistingInstance<ISessionService>() is { } sessionService)
        {
            await sessionService.SaveSessionAsync(connection).ConfigureAwait(false);
        }

        using PacketScope<Handshake> lease = PacketFactory<Handshake>.Acquire();

        Handshake reply = lease.Value;
        reply.Stage = HandshakeStage.SERVER_FINISH;
        reply.PublicKey = Bytes32.Zero;
        reply.Nonce = Bytes32.Zero;
        reply.Proof = expectedFinish;
        reply.Flags = (reply.Flags & ~PacketFlags.RELIABLE) | (packet.Flags & PacketFlags.RELIABLE);
        reply.TranscriptHash = state.TranscriptHash;
        reply.SessionToken = connection.ID.ToUInt64();

        await context.Sender.SendAsync(reply).ConfigureAwait(false);
    }

    private static async ValueTask RejectHandshakeAsync(IPacketContext<Handshake> context, ProtocolReason reason)
    {
        IConnection connection = context.Connection;

        if (connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeState, out object? removedState) && removedState is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);

        try
        {
            using PacketScope<Handshake> lease = PacketFactory<Handshake>.Acquire();

            Handshake error = lease.Value;
            error.InitializeError(reason, flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE);
            await context.Sender.SendAsync(error).ConfigureAwait(false);
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

    private static bool TryAcquireHandshakeSlot(IConnection connection, out object claimToken)
    {
        claimToken = new object();
        try
        {
            connection.Attributes.Add(ConnectionAttributes.HandshakeState, claimToken);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeState, out object? current) &&
               ReferenceEquals(current, claimToken);
    }

    private static bool TryPublishHandshakeState(IConnection connection, object claimToken, HandshakeContext state)
    {
        if (!connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeState, out object? current) ||
            !ReferenceEquals(current, claimToken))
        {
            return false;
        }

        connection.Attributes[ConnectionAttributes.HandshakeState] = state;
        return true;
    }

    #endregion Private Methods
}
