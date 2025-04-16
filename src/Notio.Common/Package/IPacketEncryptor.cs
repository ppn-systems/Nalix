using Notio.Common.Cryptography;

namespace Notio.Common.Package;

/// <summary>
/// Provides a contract for encrypting and decrypting a packet of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">
/// The packet type that implements <see cref="IPacket"/> and supports static encryption and decryption.
/// </typeparam>
public interface IPacketEncryptor<T> where T : IPacket
{
    /// <summary>
    /// Encrypts a packet of type <typeparamref name="T"/> using a specific encryption algorithm.
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
    /// A new instance of <typeparamref name="T"/> that contains the encrypted packet data.
    /// </returns>
    static abstract T Encrypt(T packet, byte[] key, EncryptionMode algorithm);

    /// <summary>
    /// Decrypts a packet of type <typeparamref name="T"/> using a specific encryption algorithm.
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
    /// A new instance of <typeparamref name="T"/> that contains the decrypted packet data.
    /// </returns>
    static abstract T Decrypt(T packet, byte[] key, EncryptionMode algorithm);
}
