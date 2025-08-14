// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Security.Cryptography;

namespace Nalix.Common.Packets.Interfaces;

/// <summary>
/// Defines the static transformation contract for a packet type, including
/// serialization, encryption/decryption, and compression/decompression.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements <see cref="IPacket"/> and provides its own static transformation methods.
/// </typeparam>
public interface IPacketTransformer<TPacket> where TPacket : IPacket
{
    // --- Serialization ---

    /// <summary>
    /// Deserializes a packet instance from the specified byte buffer.
    /// </summary>
    /// <param name="buffer">
    /// A read-only span of bytes containing the serialized packet data.
    /// </param>
    /// <returns>
    /// A new <typeparamref name="TPacket"/> instance created from the provided buffer.
    /// </returns>
    static abstract TPacket Deserialize(in System.ReadOnlySpan<System.Byte> buffer);

    // --- Encryption ---

    /// <summary>
    /// Encrypts the specified packet using the provided key and encryption algorithm.
    /// </summary>
    /// <param name="packet">
    /// The packet to encrypt.
    /// </param>
    /// <param name="key">
    /// The encryption key.
    /// </param>
    /// <param name="algorithm">
    /// The symmetric encryption algorithm to apply.
    /// </param>
    /// <returns>
    /// A new <typeparamref name="TPacket"/> instance containing the encrypted data.
    /// </returns>
    static abstract TPacket Encrypt(TPacket packet, System.Byte[] key, SymmetricAlgorithmType algorithm);

    /// <summary>
    /// Decrypts the specified packet using the provided key and encryption algorithm.
    /// </summary>
    /// <param name="packet">
    /// The packet to decrypt.
    /// </param>
    /// <param name="key">
    /// The decryption key.
    /// </param>
    /// <param name="algorithm">
    /// The symmetric encryption algorithm that was used to encrypt the packet.
    /// </param>
    /// <returns>
    /// A new <typeparamref name="TPacket"/> instance containing the decrypted data.
    /// </returns>
    static abstract TPacket Decrypt(TPacket packet, System.Byte[] key, SymmetricAlgorithmType algorithm);

    // --- Compression ---

    /// <summary>
    /// Compresses the specified packet and returns the compressed result.
    /// </summary>
    /// <param name="packet">
    /// The packet to compress.
    /// </param>
    /// <returns>
    /// A new <typeparamref name="TPacket"/> instance containing the compressed data.
    /// </returns>
    static abstract TPacket Compress(TPacket packet);

    /// <summary>
    /// Decompresses the specified packet and returns the decompressed result.
    /// </summary>
    /// <param name="packet">
    /// The packet to decompress.
    /// </param>
    /// <returns>
    /// A new <typeparamref name="TPacket"/> instance containing the decompressed data.
    /// </returns>
    static abstract TPacket Decompress(TPacket packet);
}
