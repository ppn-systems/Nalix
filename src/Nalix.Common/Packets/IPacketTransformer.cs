using Nalix.Common.Packets.Enums;
using Nalix.Common.Security.Cryptography;

namespace Nalix.Common.Packets;

/// <summary>
/// Provides a contract for encrypting and decrypting a packet of type <typeparamref name="TPacket"/>.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements <see cref="IPacket"/> and supports static encryption and decryption.
/// </typeparam>
public interface IPacketTransformer<TPacket> where TPacket : IPacket
{
    // --- Creation ---

    /// <summary>
    /// Creates a packet using strongly-typed enums.
    /// </summary>
    /// <param name="id">The unique identifier of the packet.</param>
    /// <param name="s">The string.</param>
    /// <returns>A new instance of <typeparamref name="TPacket"/>.</returns>
    static abstract TPacket Create(System.UInt16 id, System.String s);

    /// <summary>
    /// Creates a packet using strongly-typed enums.
    /// </summary>
    /// <param name="id">The unique identifier of the packet.</param>
    /// <param name="flags">The flags associated with the packet as an enum.</param>
    /// <returns>A new instance of <typeparamref name="TPacket"/>.</returns>
    static abstract TPacket Create(System.UInt16 id, PacketFlags flags);

    // --- Encryption ---

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

    // --- Compression ---

    /// <summary>
    /// Compresses a packet of type <typeparamref name="TPacket"/> into the given buffer.
    /// </summary>
    /// <param name="packet">
    /// The packet to be compressed.
    /// </param>
    /// <returns>
    /// A span of bytes containing the compressed packet data.
    /// </returns>
    static abstract TPacket Compress(TPacket packet);

    /// <summary>
    /// Decompresses a packet of type <typeparamref name="TPacket"/> from the given buffer.
    /// </summary>
    /// <param name="packet">
    /// The packet to be decompress.
    /// </param>
    /// <returns>
    /// An instance of <typeparamref name="TPacket"/> that was decompressed from the buffer.
    /// </returns>
    static abstract TPacket Decompress(TPacket packet);

    // --- Serialization ---

    /// <summary>
    /// Deserializes a packet of type <typeparamref name="TPacket"/> from the given buffer.
    /// </summary>
    /// <param name="buffer">
    /// The read-only span of bytes containing the serialized packet data.
    /// </param>
    /// <returns>
    /// An instance of <typeparamref name="TPacket"/> that was deserialized from the buffer.
    /// </returns>
    static abstract TPacket Deserialize(System.ReadOnlySpan<System.Byte> buffer);
}
