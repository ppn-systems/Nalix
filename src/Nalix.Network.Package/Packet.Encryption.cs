using Nalix.Common.Cryptography;
using Nalix.Common.Package;
using Nalix.Network.Package.Engine;

namespace Nalix.Network.Package;

public readonly partial struct Packet : IPacketEncryptor<Packet>
{
    /// <inheritdoc />
    static Packet IPacketEncryptor<Packet>.Encrypt(Packet packet, byte[] key, EncryptionType algorithm)
        => PacketGuard.Encrypt(packet, key, algorithm);

    /// <inheritdoc />
    static Packet IPacketEncryptor<Packet>.Decrypt(Packet packet, byte[] key, EncryptionType algorithm)
        => PacketGuard.Decrypt(packet, key, algorithm);
}
