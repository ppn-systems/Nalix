using Notio.Common.Cryptography;
using Notio.Common.Exceptions;
using Notio.Common.Package.Enums;
using Notio.Cryptography;
using Notio.Extensions.Primitives;

namespace Notio.Network.Package.Engine;

/// <summary>
/// Provides helper methods for encrypting and decrypting packet payloads.
/// </summary>
public static class PacketGuard
{
    /// <summary>
    /// Encrypts the payload of the given packet using the specified algorithm.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be encrypted.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">The encryption algorithm to use (e.g., XTEA, ChaCha20Poly1305).</param>
    /// <returns>A new <see cref="Packet"/> instance with the encrypted payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if encryption conditions are not met or if an error occurs during encryption.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet Encrypt(
        Packet packet, byte[] key, EncryptionMode algorithm = EncryptionMode.XTEA)
    {
        PacketOps.CheckEncryption(packet, key, isEncryption: true);

        try
        {
            System.Memory<byte> encryptedPayload = Ciphers.Encrypt(packet.Payload, key, algorithm);

            return new Packet(packet.Id, packet.Checksum, packet.Timestamp, packet.Code, packet.Type,
                packet.Flags.AddFlag(PacketFlags.Encrypted), packet.Priority, packet.Number, encryptedPayload);
        }
        catch (System.Exception ex)
        {
            throw new PackageException("Failed to encrypt the packet payload.", ex);
        }
    }

    /// <summary>
    /// Decrypts the payload of the given packet using the specified algorithm.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be decrypted.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="algorithm">The encryption algorithm that was used (e.g., XTEA, ChaCha20Poly1305).</param>
    /// <returns>A new <see cref="Packet"/> instance with the decrypted payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if decryption conditions are not met or if an error occurs during decryption.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet Decrypt(
        Packet packet, byte[] key, EncryptionMode algorithm = EncryptionMode.XTEA)
    {
        PacketOps.CheckEncryption(packet, key, isEncryption: false);

        try
        {
            System.Memory<byte> decryptedPayload = Ciphers.Decrypt(packet.Payload, key, algorithm);

            return new Packet(packet.Id, packet.Checksum, packet.Timestamp, packet.Code, packet.Type,
                packet.Flags.RemoveFlag(PacketFlags.Encrypted), packet.Priority, packet.Number, decryptedPayload);
        }
        catch (System.Exception ex)
        {
            throw new PackageException("Failed to decrypt the packet payload.", ex);
        }
    }
}
