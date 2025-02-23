using Notio.Common.Cryptography;
using Notio.Common.Exceptions;
using Notio.Cryptography;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Extensions;
using Notio.Network.Package.Utilities;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Helpers;

/// <summary>
/// Provides helper methods for encrypting and decrypting packet payloads.
/// </summary>
public static class PacketEncryptionHelper
{
    /// <summary>
    /// Encrypts the payload of the given packet using the specified algorithm.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be encrypted.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">The encryption algorithm to use (e.g., Xtea, ChaCha20Poly1305).</param>
    /// <returns>A new <see cref="Packet"/> instance with the encrypted payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if encryption conditions are not met or if an error occurs during encryption.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet EncryptPayload(Packet packet, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        PacketVerifier.CheckEncryptionConditions(packet, key, isEncryption: true);

        try
        {
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
    /// Decrypts the payload of the given packet using the specified algorithm.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be decrypted.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="algorithm">The encryption algorithm that was used (e.g., Xtea, ChaCha20Poly1305).</param>
    /// <returns>A new <see cref="Packet"/> instance with the decrypted payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if decryption conditions are not met or if an error occurs during decryption.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecryptPayload(Packet packet, byte[] key, EncryptionMode algorithm = EncryptionMode.Xtea)
    {
        PacketVerifier.CheckEncryptionConditions(packet, key, isEncryption: false);

        try
        {
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
