using Notio.Common.Connection;
using Notio.Common.Models;
using Notio.Cryptography.Ciphers.Asymmetric;
using Notio.Network.Package;
using Notio.Network.Package.Enums;
using System.Security.Cryptography;

namespace Notio.Network.Handlers;

[PacketController]
public static class PacketDefault
{
    [PacketCommand(1, Authoritys.Guests)]
    public static Packet InitiateSecureConnection(IConnection connection, Packet packet)
    {
        byte[] _x25519PublicKey;
        byte[] _x25519PrivateKey;
        (_x25519PrivateKey, _x25519PublicKey) = X25519.GenerateKeyPair();

        byte[] sharedSecret = X25519.ComputeSharedSecret(_x25519PrivateKey, packet.Payload.ToArray());

        // Derive encryption key from shared secret (e.g., using SHA-256)
        connection.EncryptionKey = SHA256.HashData(sharedSecret);

        return new Packet(PacketType.Binary, PacketFlags.None, PacketPriority.None, 0, _x25519PublicKey);
    }
}