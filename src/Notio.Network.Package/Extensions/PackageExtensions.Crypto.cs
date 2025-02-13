using Notio.Common.Enums;
using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Cryptography;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Helpers;
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
    public static IPacket EncryptPayload(this Packet packet, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        // Validate encryption conditions.
        PacketVerifier.CheckEncryptionConditions(packet, key, isEncryption: true);

        try
        {
            // Encrypt the payload using the helper class.
            Memory<byte> encryptedPayload = Cipher.Encrypt(packet.Payload, key, algorithm);

            byte newFlags = packet.Flags.AddFlag(PacketFlags.IsEncrypted);

            // Return a new IPacket with the updated payload and flags.
            packet.UpdateFlags(newFlags);
            packet.UpdatePayload(encryptedPayload);

            return packet;
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
    public static IPacket DecryptPayload(this Packet packet, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        // Validate decryption conditions.
        PacketVerifier.CheckEncryptionConditions(packet, key, isEncryption: false);

        try
        {
            // Decrypt the payload using the helper class.
            Memory<byte> decryptedPayload = Cipher.Decrypt(packet.Payload, key, algorithm);

            // Remove the encryption flag on decryption.
            byte newFlags = packet.Flags.RemoveFlag(PacketFlags.IsEncrypted);

            // Return a new IPacket with the updated payload and flags.
            packet.UpdateFlags(newFlags);
            packet.UpdatePayload(decryptedPayload);

            return packet;
        }
        catch (Exception ex)
        {
            throw new PackageException("Failed to decrypt the packet payload.", ex);
        }
    }
}
