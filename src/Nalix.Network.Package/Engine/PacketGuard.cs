using Nalix.Common.Connection;
using Nalix.Common.Cryptography;
using Nalix.Common.Exceptions;
using Nalix.Common.Package.Enums;
using Nalix.Cryptography;

namespace Nalix.Network.Package.Engine;

/// <summary>
/// Provides helper methods for encrypting and decrypting packet payloads.
/// </summary>
public static class PacketGuard
{
    /// <summary>
    /// Encrypts the payload of the given packet using the specified algorithm.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be encrypted.</param>
    /// <param name="connection">The connection object containing the encryption key.</param>
    /// <returns>A new <see cref="Packet"/> instance with the encrypted payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if encryption conditions are not met or if an error occurs during encryption.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet Encrypt(in Packet packet, IConnection connection)
        => Encrypt(packet, connection.EncryptionKey, connection.Encryption);

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
        in Packet packet, byte[] key, EncryptionType algorithm = EncryptionType.XTEA)
    {
        PacketOps.CheckEncryption(packet, key, isEncryption: true);

        try
        {
            return new Packet(
                packet.Id, packet.Number, packet.Checksum,
                packet.Timestamp, packet.Type, packet.Flags | PacketFlags.Encrypted,
                packet.Priority, Ciphers.Encrypt(packet.Payload, key, algorithm));
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
    /// <param name="connection">The connection object containing the encryption key.</param>
    /// <returns>A new <see cref="Packet"/> instance with the decrypted payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if decryption conditions are not met or if an error occurs during decryption.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet Decrypt(in Packet packet, IConnection connection)
        => Decrypt(packet, connection.EncryptionKey, connection.Encryption);

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
        in Packet packet, byte[] key, EncryptionType algorithm = EncryptionType.XTEA)
    {
        PacketOps.CheckEncryption(packet, key, isEncryption: false);

        try
        {
            return new Packet(
                packet.Id, packet.Number, packet.Checksum,
                packet.Timestamp, packet.Type, packet.Flags & ~PacketFlags.Encrypted,
                packet.Priority, Ciphers.Decrypt(packet.Payload, key, algorithm));
        }
        catch (System.Exception ex)
        {
            throw new PackageException("Failed to decrypt the packet payload.", ex);
        }
    }
}
