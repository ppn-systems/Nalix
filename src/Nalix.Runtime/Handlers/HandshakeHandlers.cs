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
using Nalix.Abstractions.Injection;
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
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Security;

namespace Nalix.Runtime.Handlers;

/// <summary>
/// Provides handlers for the default server-side X25519 handshake protocol.
/// </summary>
[PacketHandler("Nalix.Handshake")]
public static partial class HandshakeHandlers
{
    #region APIs

    /// <inheritdoc/>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketOpcode(ProtocolOpCode.SESSION_INIT)]
    [PacketPermission(PermissionLevel.NONE)]
    public static async ValueTask HandleSessionInitAsync(IPacketContext<SessionInit> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsReliable)
        {
            // This is a replayed packet, ignore silently.
            return;
        }

        SessionInit packet = context.Packet;
        IConnection connection = context.Connection;

        if (connection.Attributes.ContainsKey(ConnectionAttributes.HandshakeEstablished))
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        if (s_powPolicy != null && s_powPolicy.IsUnderAttack)
        {
            if (connection.Level < PermissionLevel.POW_VERIFIED)
            {
                // Adaptive Timeout: Expected solve time + 10 seconds network buffer
                // Time to solve is approximately 2^Difficulty hashes.
                // Assuming an average client can compute ~500 hashes per millisecond.
                int adaptiveTimeoutMs = ((1 << s_powPolicy.CurrentDifficulty) / 500) + 5000;

                using PacketScope<Control> error = PacketFactory<Control>.Acquire();
                error.Value.Initialize(ControlType.ERROR, reasonCode: ProtocolReason.POW_REQUIRED, flags: PacketFlags.SYSTEM);
                await context.Sender.SendAsync(error.Value).ConfigureAwait(false);

                connection.UpdateIdleTimeout(adaptiveTimeoutMs);

                // Do NOT disconnect, just return to wait for POW_PROOF.
                return;
            }
        }

        if (!TryAcquireHandshakeSlot(connection, out object claimToken))
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        Bytes32 sharedSecretEE;
        try
        {
            sharedSecretEE = X25519.Agreement(serverKey.PrivateKey, packet.PublicKey);
        }
        catch (InvalidOperationException)
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        if (sharedSecretEE.IsZero)
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        Bytes32 sharedSecretSE;
        try
        {
            sharedSecretSE = X25519.Agreement(s_certificate, packet.PublicKey);
        }
        catch (InvalidOperationException)
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        if (sharedSecretSE.IsZero)
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.DECRYPTION_FAILED).ConfigureAwait(false);
            return;
        }

        Bytes32 masterSecret = HandshakeX25519.ComputeMasterSecret(sharedSecretEE, sharedSecretSE);

        Bytes32 serverNonce = new(Csprng.GetBytes(Bytes32.Size));

        Bytes32 transcriptHash = HandshakeX25519.ComputeTranscriptHash(
            packet.PublicKey,
            packet.Nonce,
            serverKey.PublicKey,
            serverNonce);

        HandshakeContext state = s_pool.Get<HandshakeContext>();
        state.SharedSecret = masterSecret;
        state.TranscriptHash = transcriptHash;
        state.SessionKey = HandshakeX25519.DeriveSessionKey(masterSecret, packet.Nonce, serverNonce, transcriptHash);

        if (!TryPublishHandshakeState(connection, claimToken, state))
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        using PacketScope<SessionChallenge> lease = PacketFactory<SessionChallenge>.Acquire();
        SessionChallenge reply = lease.Value;

        reply.Initialize(serverKey.PublicKey, serverNonce, HandshakeX25519.ComputeServerProof(masterSecret, transcriptHash));
        reply.SequenceId = packet.SequenceId;

        await context.Sender.SendAsync(reply).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    [ReservedOpcodePermitted]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode((ushort)ProtocolOpCode.SESSION_PROOF)]
    public static async ValueTask HandleSessionProofAsync(IPacketContext<SessionProof> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsReliable)
        {
            // This is a replayed packet, ignore silently.
            return;
        }

        SessionProof packet = context.Packet;
        IConnection connection = context.Connection;

        if (connection.Attributes.ContainsKey(ConnectionAttributes.HandshakeEstablished))
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        if (s_powPolicy != null && s_powPolicy.IsUnderAttack)
        {
            if (connection.Level < PermissionLevel.POW_VERIFIED)
            {
                using PacketScope<Control> error = PacketFactory<Control>.Acquire();

                error.Value.Initialize(ControlType.ERROR, reasonCode: ProtocolReason.POW_REQUIRED, flags: PacketFlags.SYSTEM);
                await context.Sender.SendAsync(error.Value).ConfigureAwait(false);
                return;
            }
        }

        if (!TryGetState(connection, out HandshakeContext? state) || state is null)
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.STATE_VIOLATION).ConfigureAwait(false);
            return;
        }

        Bytes32 expectedProof = HandshakeX25519.ComputeClientProof(state.SharedSecret, state.TranscriptHash);
        if (packet.Proof != expectedProof)
        {
            await RejectHandshakeAsync(connection, context.Sender, ProtocolReason.SIGNATURE_INVALID).ConfigureAwait(false);
            return;
        }

        connection.Secret = state.SessionKey;
        connection.Algorithm = CipherSuiteType.Chacha20Poly1305;

        Bytes32 expectedFinish = HandshakeX25519.ComputeServerFinishProof(state.SharedSecret, state.TranscriptHash);

        if (context.Connection.Level < PermissionLevel.ESTABLISHED)
        {
            connection.Level = PermissionLevel.ESTABLISHED;
        }

        connection.Attributes[ConnectionAttributes.HandshakeEstablished] = true;
        if (connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeState, out object? removedState) && removedState is HandshakeContext contextState)
        {
            s_pool.Return(contextState);
        }
        _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);

        if (s_sessionService != null)
        {
            await s_sessionService.SaveSessionAsync(connection).ConfigureAwait(false);
        }

        using PacketScope<SessionEstablished> lease = PacketFactory<SessionEstablished>.Acquire();
        SessionEstablished reply = lease.Value;

        reply.Initialize(expectedFinish, connection.ID);
        reply.SequenceId = packet.SequenceId;

        await context.Sender.SendAsync(reply).ConfigureAwait(false);
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

            LoadCertificate(Path.Combine(Directories.ConfigurationDirectory, "certificate.private"));
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
            LoadCertificate(path);
            Volatile.Write(ref s_isInitialized, 1);
        }
    }

    /// <summary>
    /// Configures the internal object pool limits for handshake contexts.
    /// </summary>
    /// <param name="maxCapacity">The maximum number of contexts to retain in the pool.</param>
    /// <param name="preallocCount">The number of contexts to preallocate immediately.</param>
    public static void ConfigureContextPool(int maxCapacity, int preallocCount = 0)
    {
        _ = s_pool.SetMaxCapacity<HandshakeContext>(maxCapacity);

        if (preallocCount > 0)
        {
            _ = s_pool.Prealloc<HandshakeContext>(preallocCount);
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

    [Inject]
    private static ObjectPoolManager s_pool = null!;

    [Inject]
    private static ISessionService? s_sessionService;

    [Inject]
    private static IProofOfWorkPolicy? s_powPolicy;

    #endregion Fields

    #region Private Methods

    #region Nested Types

    private sealed class HandshakeContext : IPoolable
    {
        public Bytes32 SessionKey;
        public Bytes32 SharedSecret;
        public Bytes32 TranscriptHash;

        public void ResetForPool()
        {
            MemorySecurity.ZeroMemory(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref SessionKey, 1)));
            MemorySecurity.ZeroMemory(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref SharedSecret, 1)));
            MemorySecurity.ZeroMemory(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref TranscriptHash, 1)));
        }
    }

    #endregion Nested Types

    private static ICertificateStore GetCertificateStore() =>
        InstanceManager.Instance.GetExistingInstance<ICertificateStore>() ??
        InstanceManager.Instance.GetOrCreateInstance<FileCertificateStore>();

    private static void LoadCertificate(string certPath)
    {
        ICertificateStore store = GetCertificateStore();
        try
        {
            s_certificate = store.Load(certPath);
            s_serverPublicKey = X25519.GenerateKeyFromPrivateKey(s_certificate).PublicKey;
        }
        catch (Exception ex) when (ex is not InternalErrorException)
        {
            throw new InternalErrorException($"Handshake failed: Unable to load server identity from '{certPath}'. Exception detail: " + ex.Message, ex);
        }
    }

    private static async ValueTask RejectHandshakeAsync(IConnection connection, IPacketSender sender, ProtocolReason reason)
    {
        if (connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeState, out object? removedState) && removedState is HandshakeContext contextState)
        {
            s_pool.Return(contextState);
        }

        _ = connection.Attributes.Remove(ConnectionAttributes.HandshakeState);

        try
        {
            using Control error = new();
            error.Initialize(ControlType.ERROR, reasonCode: reason, flags: PacketFlags.SYSTEM);

            await sender.SendAsync(error).ConfigureAwait(false);
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
