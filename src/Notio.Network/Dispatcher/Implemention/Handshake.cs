using Notio.Common.Attributes;
using Notio.Common.Connection;
using Notio.Common.Cryptography.Asymmetric;
using Notio.Common.Cryptography.Hashing;
using Notio.Common.Logging;
using Notio.Common.Package;
using Notio.Common.Security;
using System;
using System.Linq;

namespace Notio.Network.Dispatcher.Implemention;

/// <summary>
/// Handles the secure handshake process for establishing encrypted connections using X25519 and ISha.
/// This class manages both the initiation and finalization of secure connections with clients.
/// </summary>
[PacketController]
public class Handshake
{
    private readonly ISha _sha;
    private readonly IX25519 _x25519;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Handshake"/> class.
    /// </summary>
    /// <param name="sha">The hashing algorithm implementation to use (e.g., SHA-256).</param>
    /// <param name="x25519">The X25519 implementation for key exchange.</param>
    /// <param name="logger">The logger for recording events and errors (optional).</param>
    public Handshake(ISha sha, IX25519 x25519, ILogger? logger)
    {
        _sha = sha;
        _x25519 = x25519;
        _logger = logger;

        _sha.Initialize(); // Initialize the hashing algorithm
    }

    /// <summary>
    /// Initiates a secure connection by performing a handshake with the client.
    /// Expects a binary packet containing the client's X25519 public key (32 bytes).
    /// </summary>
    /// <param name="packet">The incoming packet containing the client's public key.</param>
    /// <param name="connection">The connection to the client.</param>
    [PacketPermission(PermissionLevel.Guest)]
    [PacketCommand((ushort)ProtocolCommand.InitiateHandshake)]
    public void InitiateHandshake(IPacket packet, IConnection connection)
    {
        string address = connection.RemoteEndPoint;

        if (packet.Type != PacketType.Binary)
        {
            _logger?.Warn(
                $"Received non-binary packet type {packet.Type} " +
                $"from connection {connection.RemoteEndPoint}");

            PacketSender.StringPacket(connection, "Unsupported packet type.", 0);
            return;
        }

        if (packet.Payload.Length != 32) // X25519 public key must be 32 bytes
        {
            _logger?.Warn(
                $"Invalid public key length {packet.Payload.Length} " +
                $"from connection {connection.RemoteEndPoint}");

            PacketSender.StringPacket(connection, "Invalid public key.", 0);
            return;
        }

        try
        {
            // Generate an X25519 key pair
            (byte[] privateKey, byte[] publicKey) = _x25519.Generate();

            connection.Metadata["X25519_PrivateKey"] = privateKey;
            connection.EncryptionKey = this.GenerateSharedSecret(privateKey, packet.Payload.ToArray());

            // Send the server's public key back to the client
            if (PacketSender.BinaryPacket(connection, publicKey, 1))
            {
                // Elevate the client's access level
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
            _logger?.Error($"Failed to initiate secure connection for connection {address}", ex);

            PacketSender.StringPacket(connection, "Internal error during secure connection initiation.", 0);
        }
    }

    /// <summary>
    /// Finalizes the secure connection by verifying the client's public key and 
    /// comparing it to the derived encryption key.
    /// </summary>
    /// <param name="packet">The incoming packet containing the client's public key for finalization.</param>
    /// <param name="connection">The connection to the client.</param>
    [PacketPermission(PermissionLevel.Guest)]
    [PacketCommand((ushort)ProtocolCommand.CompleteHandshake)]
    public void CompleteHandshake(IPacket packet, IConnection connection)
    {
        if (packet.Type != PacketType.Binary)
        {
            PacketSender.StringPacket(connection, "Unsupported packet type.", 0);
            return;
        }
        if (packet.Payload.Length != 32)
        {
            _logger?.Warn(
                $"Invalid public key length {packet.Payload.Length} from connection {connection.RemoteEndPoint}");

            PacketSender.StringPacket(connection, "Invalid public key.", 0);
            return;
        }

        if (!connection.Metadata.TryGetValue("X25519_PrivateKey", out object? privateKeyObj) ||
            privateKeyObj is not byte[] privateKey)
        {
            _logger?.Warn($"Missing or invalid X25519 private key for connection {connection.RemoteEndPoint}");
            PacketSender.StringPacket(connection, "Invalid public key.", 0);
            return;
        }

        try
        {
            byte[] derivedKey = this.GenerateSharedSecret(privateKey, packet.Payload.ToArray());

            if (connection.EncryptionKey.SequenceEqual(derivedKey))
            {
                _logger?.Info($"Secure connection finalized successfully for connection {connection.RemoteEndPoint}");
                PacketSender.StringPacket(connection, "Secure connection established.", 0);
            }
            else
            {
                _logger?.Warn($"Key mismatch during finalization for connection {connection.RemoteEndPoint}");
                PacketSender.StringPacket(connection, "Key mismatch.", 0);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Failed to finalize secure connection for connection {connection.RemoteEndPoint}", ex);
            PacketSender.StringPacket(connection, "Internal error during secure connection finalization.", 0);
        }
    }

    /// <summary>
    /// Computes a derived key by performing the X25519 key exchange and then hashing the result.
    /// </summary>
    /// <param name="privateKey">The server's private key.</param>
    /// <param name="publicKey">The client's public key.</param>
    /// <returns>The derived encryption key.</returns>
    private byte[] GenerateSharedSecret(byte[] privateKey, byte[] publicKey)
    {
        byte[] secret = _x25519.Compute(privateKey, publicKey);
        _sha.Update(secret);
        return _sha.FinalizeHash();
    }
}
