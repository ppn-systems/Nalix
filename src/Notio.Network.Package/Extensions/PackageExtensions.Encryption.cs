using Notio.Common.Cryptography;
using Notio.Network.Package.Helpers;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Provides encryption and decryption methods for IPacket Payload.
/// </summary>
public static partial class PackageExtensions
{
    /// <summary>
    /// Encrypts the Payload in the IPacket using the specified algorithm.
    /// (Mã hóa Payload trong IPacket sử dụng thuật toán chỉ định.)
    /// </summary>
    /// <param name="packet">The packet to be encrypted.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">The encryption algorithm to use (e.g., XTEA, AesGcm, ChaCha20Poly1305).</param>
    /// <returns>A new IPacket instance with the encrypted payload.</returns>
    public static Packet EncryptPayload(this Packet packet,
        byte[] key, EncryptionMode algorithm = EncryptionMode.XTEA)
            => PacketEncryptionHelper.EncryptPayload(packet, key, algorithm);

    /// <summary>
    /// Decrypts the Payload in the IPacket using the specified algorithm.
    /// (Giải mã Payload trong IPacket sử dụng thuật toán chỉ định.)
    /// </summary>
    /// <param name="packet">The packet to be decrypted.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="algorithm">The encryption algorithm that was used (e.g., XTEA, AesGcm, ChaCha20Poly1305).</param>
    /// <returns>A new IPacket instance with the decrypted payload.</returns>
    public static Packet DecryptPayload(this Packet packet,
        byte[] key, EncryptionMode algorithm = EncryptionMode.XTEA)
            => PacketEncryptionHelper.DecryptPayload(packet, key, algorithm);
}
