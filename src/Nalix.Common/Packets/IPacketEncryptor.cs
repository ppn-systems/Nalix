using Nalix.Common.Security.Cryptography;

namespace Nalix.Common.Packets;

/// <summary>
/// Provides a contract for encrypting and decrypting a packet of type <typeparamref name="TPacket"/>.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements <see cref="IPacket"/> and supports static encryption and decryption.
/// </typeparam>
public interface IPacketEncryptor<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Encrypts a packet of type <typeparamref name="TPacket"/> using a specific encryption algorithm.
    /// </summary>
    /// <param name="packet">
    /// The packet to be encrypted.
    /// </param>
    /// <param name="key">
    /// The encryption key.
    /// </param>
    /// <param name="algorithm">
    /// The encryption algorithm to use for the packet's payload.
    /// </param>
    /// <returns>
    /// A new instance of <typeparamref name="TPacket"/> that contains the encrypted packet data.
    /// </returns>
    static abstract TPacket Encrypt(TPacket packet, System.Byte[] key, SymmetricAlgorithmType algorithm);

    /// <summary>
    /// Decrypts a packet of type <typeparamref name="TPacket"/> using a specific encryption algorithm.
    /// </summary>
    /// <param name="packet">
    /// The packet to be decrypted.
    /// </param>
    /// <param name="key">
    /// The encryption key.
    /// </param>
    /// <param name="algorithm">
    /// The encryption algorithm used to decrypt the packet's payload.
    /// </param>
    /// <returns>
    /// A new instance of <typeparamref name="TPacket"/> that contains the decrypted packet data.
    /// </returns>
    static abstract TPacket Decrypt(TPacket packet, System.Byte[] key, SymmetricAlgorithmType algorithm);
}
