using Notio.Common.Cryptography;
using Notio.Common.Package;

namespace Notio.Network.Package;

public readonly partial struct Packet
{
    /// <inheritdoc />
    static Packet IPacketEncryptor<Packet>.Encrypt(Packet packet, byte[] key, EncryptionMode algorithm)
        => Utilities.PacketEncryption.EncryptPayload(packet, key, algorithm);

    /// <inheritdoc />
    static Packet IPacketEncryptor<Packet>.Decrypt(Packet packet, byte[] key, EncryptionMode algorithm)
        => Utilities.PacketEncryption.DecryptPayload(packet, key, algorithm);
}
