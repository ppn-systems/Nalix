using Nalix.Common.Connection;
using Nalix.Common.Constants;
using Nalix.Common.Cryptography.Asymmetric;
using Nalix.Common.Cryptography.Hashing;
using Nalix.Common.Logging;
using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;
using Nalix.Common.Package.Enums;
using Nalix.Common.Security;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Net.Controller;

/// <summary>
/// Handles the secure handshake process for establishing encrypted connections using X25519 and ISHA.
/// This class manages both the initiation and finalization of secure connections with clients.
/// The class ensures secure communication by exchanging keys and validating them using X25519 and hashing via ISHA.
/// </summary>
[PacketController]
public sealed class HandshakeController<TPacket> where TPacket : IPacket, IPacketFactory<TPacket>
{
    #region Fields

    private readonly ILogger? _logger;
    private readonly ISHA _hashAlgorithm;
    private readonly IX25519 _keyExchangeAlgorithm;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HandshakeController{TPacket}"/> class with necessary components.
    /// </summary>
    /// <param name="sha">The hashing algorithm implementation to use (e.g., SHA-256).</param>
    /// <param name="x25519">The X25519 implementation for key exchange.</param>
    /// <param name="logger">Optional logger for recording events and errors during the handshake process.</param>
    public HandshakeController(ISHA sha, IX25519 x25519, ILogger? logger)
    {
        _logger = logger;
        _hashAlgorithm = sha;
        _keyExchangeAlgorithm = x25519;

        _hashAlgorithm.Initialize(); // Initialize the hashing algorithm.
    }

    #endregion Constructors

    /// <summary>
    /// Initiates a secure connection by performing a handshake with the client.
    /// Expects a binary packet containing the client's X25519 public key (32 bytes).
    /// </summary>
    /// <param name="packet">The incoming packet containing the client's public key.</param>
    /// <param name="connection">The connection to the client that is requesting the handshake.</param>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Moderate)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(HandshakeController<TPacket>))]
    [PacketId((ushort)ConnectionCommand.StartHandshake)]
    [PacketRateLimit(MaxRequests = 1, LockoutDurationSeconds = 120)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal System.Memory<byte> StartHandshake(IPacket packet, IConnection connection)
    {
        // CheckLimit if the packet type is binary (as expected for X25519 public key).
        if (packet.Type != PacketType.Binary)
        {
            _logger?.Debug("Received non-binary packet [Type={0}] from {1}",
                           packet.Type, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ConnectionCommand.StartHandshake,
                PacketCode.PacketType).Serialize();
        }

        // Validate that the public key length is 32 bytes (X25519 standard).
        if (packet.Payload.Length != 32)
        {
            _logger?.Debug("Invalid public key length [Length={0}] from {1}",
                           packet.Payload.Length, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ConnectionCommand.StartHandshake,
                PacketCode.InvalidPayload).Serialize();
        }

        if (IsReplayAttempt(connection))
        {
            _logger?.Debug("Detected handshake replay attempt from {0}", connection.RemoteEndPoint);
            return TPacket.Create((ushort)ConnectionCommand.CompleteHandshake, PacketCode.RateLimited).Serialize();
        }

        // Generate an X25519 key pair (private and public keys).
        (byte[] @private, byte[] @public) = _keyExchangeAlgorithm.Generate();

        // Store the private key in connection metadata for later use.
        connection.Metadata[Meta.PrivateKey] = @private;
        connection.Metadata[Meta.LastHandshakeTime] = System.DateTime.UtcNow;

        // Derive the shared secret key using the server's private key and the client's public key.
        connection.EncryptionKey = DeriveSharedKey(@private, packet.Payload.ToArray());

        // Elevate the client's access level after successful handshake initiation.
        connection.Level = PermissionLevel.User;

        // SendPacket the server's public key back to the client for the next phase of the handshake.
        return TPacket.Create(
            (ushort)ConnectionCommand.StartHandshake, PacketCode.Success,
            PacketType.Binary, PacketFlags.None, PacketPriority.Low, @public).Serialize();
    }

    /// <summary>
    /// Finalizes the secure connection by verifying the client's public key and comparing it to the derived encryption key.
    /// This method ensures the integrity of the handshake process by performing key comparison.
    /// </summary>
    /// <param name="packet">The incoming packet containing the client's public key for finalization.</param>
    /// <param name="connection">The connection to the client.</param>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Moderate)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(HandshakeController<TPacket>))]
    [PacketId((ushort)ConnectionCommand.CompleteHandshake)]
    [PacketRateLimit(MaxRequests = 1, LockoutDurationSeconds = 120)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal System.Memory<byte> CompleteHandshake(IPacket packet, IConnection connection)
    {
        // Ensure the packet type is binary (expected for public key).
        if (packet.Type != PacketType.Binary)
        {
            _logger?.Debug("Received non-binary packet [Type={0}] from {1}",
                           packet.Type, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ConnectionCommand.CompleteHandshake, PacketCode.PacketType).Serialize();
        }

        // CheckLimit if the public key length is correct (32 bytes).
        if (packet.Payload.Length != 32)
        {
            _logger?.Debug("Invalid public key length [Length={0}] from {1}",
                           packet.Payload.Length, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ConnectionCommand.CompleteHandshake, PacketCode.InvalidPayload).Serialize();
        }

        // Retrieve the stored private key from connection metadata.
        if (!connection.Metadata.TryGetValue(Meta.PrivateKey, out object? privateKeyObj) ||
            privateKeyObj is not byte[] @private)
        {
            _logger?.Debug("Missing or invalid private key for {0}", connection.RemoteEndPoint);

            return TPacket.Create((ushort)ConnectionCommand.CompleteHandshake, PacketCode.UnknownError).Serialize();
        }

        // Derive the shared secret using the private key and the client's public key.
        byte[] derivedKey = DeriveSharedKey(@private, packet.Payload.ToArray());

        connection.Metadata.Remove(Meta.PrivateKey);

        // Compare the derived key with the encryption key in the connection.
        if (connection.EncryptionKey is null || !connection.EncryptionKey.SequenceEqual(derivedKey))
        {
            _logger?.Debug("Key mismatch during handshake finalization for {0}", connection.RemoteEndPoint);
            return TPacket.Create(
                (ushort)ConnectionCommand.CompleteHandshake,
                PacketCode.Conflict).Serialize();
        }

        _logger?.Debug("Secure connection established for {0}", connection.RemoteEndPoint);
        return TPacket.Create(
                (ushort)ConnectionCommand.CompleteHandshake,
                PacketCode.Success).Serialize();
    }

    #region Private Methods

    /// <summary>
    /// Computes a derived encryption key by performing the X25519 key exchange and then hashing the result.
    /// This method produces a shared secret by combining the client's public key and the server's private key,
    /// followed by hashing the result using the specified hashing algorithm.
    /// </summary>
    /// <param name="privateKey">The server's private key used in the key exchange.</param>
    /// <param name="publicKey">The client's public key involved in the key exchange.</param>
    /// <returns>The derived encryption key, which is used to establish a secure connection.</returns>
    private byte[] DeriveSharedKey(byte[] privateKey, byte[] publicKey)
    {
        // Perform the X25519 key exchange to derive the shared secret.
        byte[] secret = _keyExchangeAlgorithm.Compute(privateKey, publicKey);

        // Hash the shared secret to produce the final encryption key.
        _hashAlgorithm.Update(secret);
        return _hashAlgorithm.FinalizeHash();
    }

    /// <summary>
    /// Checks if the connection is attempting to replay a previous handshake.
    /// </summary>
    /// <param name="connection"></param>
    /// <returns></returns>
    private static bool IsReplayAttempt(IConnection connection)
    {
        if (connection.Metadata.TryGetValue(Meta.LastHandshakeTime,
            out object? lastTimeObj) && lastTimeObj is System.DateTime lastTime)
            return (System.DateTime.UtcNow - lastTime).TotalSeconds < 10;

        return false;
    }

    private static class Meta
    {
        public const string PrivateKey = "X25519_PrivateKey";
        public const string LastHandshakeTime = "LastHandshakeTime";
    }

    #endregion Private Methods
}
