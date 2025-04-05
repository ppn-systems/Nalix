using Notio.Common.Attributes;
using Notio.Common.Connection;
using Notio.Common.Cryptography.Asymmetric;
using Notio.Common.Cryptography.Hashing;
using Notio.Common.Logging;
using Notio.Common.Package;
using Notio.Common.Security;
using Notio.Network.Core;
using System;
using System.Linq;

namespace Notio.Network.Dispatcher.BuiltIn;

/// <summary>
/// Handles the secure handshake process for establishing encrypted connections using X25519 and ISha.
/// This class manages both the initiation and finalization of secure connections with clients.
/// The class ensures secure communication by exchanging keys and validating them using X25519 and hashing via ISha.
/// </summary>
[PacketController]
public class Handshake
{
    private readonly ILogger? _logger;
    private readonly ISha _hashAlgorithm;
    private readonly IX25519 _keyExchangeAlgorithm;

    /// <summary>
    /// Initializes a new instance of the <see cref="Handshake"/> class with necessary components.
    /// </summary>
    /// <param name="sha">The hashing algorithm implementation to use (e.g., SHA-256).</param>
    /// <param name="x25519">The X25519 implementation for key exchange.</param>
    /// <param name="logger">Optional logger for recording events and errors during the handshake process.</param>
    public Handshake(ISha sha, IX25519 x25519, ILogger? logger)
    {
        _hashAlgorithm = sha;
        _keyExchangeAlgorithm = x25519;
        _logger = logger;

        _hashAlgorithm.Initialize(); // Initialize the hashing algorithm.
    }

    /// <summary>
    /// Initiates a secure connection by performing a handshake with the client.
    /// Expects a binary packet containing the client's X25519 public key (32 bytes).
    /// </summary>
    /// <param name="packet">The incoming packet containing the client's public key.</param>
    /// <param name="connection">The connection to the client that is requesting the handshake.</param>
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)ProtocolCommand.InitiateHandshake)]
    public void InitiateHandshake(IPacket packet, IConnection connection)
    {
        string address = connection.RemoteEndPoint;

        // Check if the packet type is binary (as expected for X25519 public key).
        if (packet.Type != PacketType.Binary)
        {
            _logger?.Warn(
                $"Received non-binary packet type {packet.Type} " +
                $"from connection {connection.RemoteEndPoint}");

            PacketTransmitter.SendString(connection, "Unsupported packet type.", 0);
            return;
        }

        // Validate that the public key length is 32 bytes (X25519 standard).
        if (packet.Payload.Length != 32)
        {
            _logger?.Warn(
                $"Invalid public key length {packet.Payload.Length} " +
                $"from connection {connection.RemoteEndPoint}");

            PacketTransmitter.SendString(connection, "Invalid public key.", 0);
            return;
        }

        try
        {
            // Generate an X25519 key pair (private and public keys).
            (byte[] privateKey, byte[] publicKey) = _keyExchangeAlgorithm.Generate();

            // Store the private key in connection metadata for later use.
            connection.Metadata["X25519_PrivateKey"] = privateKey;

            // Derive the shared secret key using the server's private key and the client's public key.
            connection.EncryptionKey = this.GenerateEncryptionKeyFromKeys(privateKey, packet.Payload.ToArray());

            // Send the server's public key back to the client for the next phase of the handshake.
            if (PacketTransmitter.SendBinary(connection, publicKey, 1))
            {
                // Elevate the client's access level after successful handshake initiation.
                connection.Authority = PermissionLevel.User;
                _logger?.Info($"Secure connection initiated successfully for connection {address}");
            }
            else
            {
                _logger?.Error($"Failed to send public key response to connection {address}");
            }
        }
        catch (Exception ex)
        {
            // Log any errors that occur during the handshake process.
            _logger?.Error($"Failed to initiate secure connection for connection {address}", ex);

            PacketTransmitter.SendString(connection, "Internal error during secure connection initiation.", 0);
        }
    }

    /// <summary>
    /// Finalizes the secure connection by verifying the client's public key and comparing it to the derived encryption key.
    /// This method ensures the integrity of the handshake process by performing key comparison.
    /// </summary>
    /// <param name="packet">The incoming packet containing the client's public key for finalization.</param>
    /// <param name="connection">The connection to the client.</param>
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)ProtocolCommand.CompleteHandshake)]
    public void FinalizeHandshake(IPacket packet, IConnection connection)
    {
        // Ensure the packet type is binary (expected for public key).
        if (packet.Type != PacketType.Binary)
        {
            PacketTransmitter.SendString(connection, "Unsupported packet type.", 0);
            return;
        }

        // Check if the public key length is correct (32 bytes).
        if (packet.Payload.Length != 32)
        {
            _logger?.Warn(
                $"Invalid public key length {packet.Payload.Length} from connection {connection.RemoteEndPoint}");

            PacketTransmitter.SendString(connection, "Invalid public key.", 0);
            return;
        }

        // Retrieve the stored private key from connection metadata.
        if (!connection.Metadata.TryGetValue("X25519_PrivateKey", out object? privateKeyObj) ||
            privateKeyObj is not byte[] privateKey)
        {
            _logger?.Warn($"Missing or invalid X25519 private key for connection {connection.RemoteEndPoint}");
            PacketTransmitter.SendString(connection, "Invalid public key.", 0);
            return;
        }

        try
        {
            // Derive the shared secret using the private key and the client's public key.
            byte[] derivedKey = this.GenerateEncryptionKeyFromKeys(privateKey, packet.Payload.ToArray());

            // Compare the derived key with the encryption key in the connection.
            if (connection.EncryptionKey.SequenceEqual(derivedKey))
            {
                _logger?.Info($"Secure connection finalized successfully for connection {connection.RemoteEndPoint}");
                PacketTransmitter.SendString(connection, "Secure connection established.", 0);
            }
            else
            {
                _logger?.Warn($"Key mismatch during finalization for connection {connection.RemoteEndPoint}");
                PacketTransmitter.SendString(connection, "Key mismatch.", 0);
            }
        }
        catch (Exception ex)
        {
            // Log any errors that occur during the finalization of the handshake.
            _logger?.Error($"Failed to finalize secure connection for connection {connection.RemoteEndPoint}", ex);
            PacketTransmitter.SendString(connection, "Internal error during secure connection finalization.", 0);
        }
    }

    /// <summary>
    /// Computes a derived encryption key by performing the X25519 key exchange and then hashing the result.
    /// This method produces a shared secret by combining the client's public key and the server's private key,
    /// followed by hashing the result using the specified hashing algorithm.
    /// </summary>
    /// <param name="privateKey">The server's private key used in the key exchange.</param>
    /// <param name="publicKey">The client's public key involved in the key exchange.</param>
    /// <returns>The derived encryption key, which is used to establish a secure connection.</returns>
    private byte[] GenerateEncryptionKeyFromKeys(byte[] privateKey, byte[] publicKey)
    {
        // Perform the X25519 key exchange to derive the shared secret.
        byte[] secret = _keyExchangeAlgorithm.Compute(privateKey, publicKey);

        // Hash the shared secret to produce the final encryption key.
        _hashAlgorithm.Update(secret);
        return _hashAlgorithm.FinalizeHash();
    }
}
