using Nalix.Common.Connection;
using Nalix.Common.Exceptions;
using Nalix.Common.Package.Enums;
using Nalix.Common.Security.Cryptography;
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
        in Packet packet, System.Byte[] key, SymmetricAlgorithmType algorithm = SymmetricAlgorithmType.XTEA)
    {
        if (packet.Payload.IsEmpty)
        {
            throw new PackageException("Payload is empty and cannot be processed.");
        }

        if ((packet.Flags & PacketFlags.Encrypted) == 0)
        {
            throw new PackageException("Payload is not encrypted and cannot be decrypted.");
        }

        try
        {
            return new Packet(
                packet.OpCode, packet.Number, packet.Checksum,
                packet.Timestamp, packet.Type, packet.Flags | PacketFlags.Encrypted,
                packet.Priority, Ciphers.Encrypt(packet.Payload, key, algorithm));
        }
        catch (System.Exception ex)
        {
            throw new PackageException("Failed to encrypt the packet payload.", ex);
        }
        finally
        {
            // Dispose the original packet payload to free resources
            packet.Dispose();
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
        in Packet packet, System.Byte[] key, SymmetricAlgorithmType algorithm = SymmetricAlgorithmType.XTEA)
    {
        if (packet.Payload.IsEmpty)
        {
            throw new PackageException("Payload is empty and cannot be processed.");
        }

        if ((packet.Flags & PacketFlags.Encrypted) == 0)
        {
            throw new PackageException("Payload is not encrypted and cannot be decrypted.");
        }

        try
        {
            return new Packet(
                packet.OpCode, packet.Number, packet.Checksum,
                packet.Timestamp, packet.Type, packet.Flags & ~PacketFlags.Encrypted,
                packet.Priority, Ciphers.Decrypt(packet.Payload, key, algorithm));
        }
        catch (System.Exception ex)
        {
            throw new PackageException("Failed to decrypt the packet payload.", ex);
        }
        finally
        {
            // Dispose the original packet payload to free resources
            packet.Dispose();
        }
    }
}