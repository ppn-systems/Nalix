using Notio.Common.Enums;
using Notio.Common.Exceptions;
using Notio.Cryptography;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Utilities;
using System;

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
    /// <param name="algorithm">The encryption algorithm to use (e.g., Xtea, AesGcm, ChaCha20Poly1305).</param>
    /// <returns>A new IPacket instance with the encrypted payload.</returns>
    public static Packet EncryptPayload(this Packet packet, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        // Validate encryption conditions.
        PacketVerifier.CheckEncryptionConditions(packet, key, isEncryption: true);

        try
        {
            // Encrypt the payload using the helper class.
            Memory<byte> encryptedPayload = Ciphers.Encrypt(packet.Payload, key, algorithm);

            return new Packet(packet.Id, packet.Type, packet.Flags.AddFlag(PacketFlags.IsEncrypted),
                packet.Priority, packet.Command, packet.Timestamp, packet.Checksum, encryptedPayload);
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to encrypt the packet payload.", ex);
        }
    }

    /// <summary>
    /// Decrypts the Payload in the IPacket using the specified algorithm.
    /// (Giải mã Payload trong IPacket sử dụng thuật toán chỉ định.)
    /// </summary>
    /// <param name="packet">The packet to be decrypted.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="algorithm">The encryption algorithm that was used (e.g., Xtea, AesGcm, ChaCha20Poly1305).</param>
    /// <returns>A new IPacket instance with the decrypted payload.</returns>
    public static Packet DecryptPayload(this Packet packet, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        // Validate decryption conditions.
        PacketVerifier.CheckEncryptionConditions(packet, key, isEncryption: false);

        try
        {
            // Decrypt the payload using the helper class.
            Memory<byte> decryptedPayload = Ciphers.Decrypt(packet.Payload, key, algorithm);

            return new Packet(packet.Id, packet.Type, packet.Flags.RemoveFlag(PacketFlags.IsEncrypted),
                packet.Priority, packet.Command, packet.Timestamp, packet.Checksum, decryptedPayload);
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to decrypt the packet payload.", ex);
        }
    }
}
