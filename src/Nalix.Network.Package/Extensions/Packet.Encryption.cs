using Nalix.Common.Cryptography;
using Nalix.Network.Package.Engine;

namespace Nalix.Network.Package.Extensions;

/// <summary>
/// Provides encryption and decryption methods for IPacket Payload.
/// </summary>
public static class PacketEncryption
{
    /// <summary>
    /// Encrypts the Payload in the IPacket using the specified algorithm.
    /// (Mã hóa Payload trong IPacket sử dụng thuật toán chỉ định.)
    /// </summary>
    /// <param name="packet">The packet to be encrypted.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">The encryption algorithm to use (e.g., XTEA, AesGcm, ChaCha20Poly1305).</param>
    /// <returns>A new IPacket instance with the encrypted payload.</returns>
    public static Packet EncryptPayload(this Packet packet, byte[] key, EncryptionType algorithm = EncryptionType.XTEA)
        => PacketGuard.Encrypt(packet, key, algorithm);

    /// <summary>
    /// Decrypts the Payload in the IPacket using the specified algorithm.
    /// (Giải mã Payload trong IPacket sử dụng thuật toán chỉ định.)
    /// </summary>
    /// <param name="packet">The packet to be decrypted.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="algorithm">The encryption algorithm that was used (e.g., XTEA, AesGcm, ChaCha20Poly1305).</param>
    /// <returns>A new IPacket instance with the decrypted payload.</returns>
    public static Packet DecryptPayload(this Packet packet, byte[] key, EncryptionType algorithm = EncryptionType.XTEA)
        => PacketGuard.Decrypt(packet, key, algorithm);
}
