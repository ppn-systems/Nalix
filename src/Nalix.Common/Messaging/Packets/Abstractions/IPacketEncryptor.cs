// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Common.Messaging.Packets.Abstractions;

/// <summary>
/// Exposes static encryption/decryption capability for a packet type.
/// </summary>
/// <typeparam name="TPacket">Packet type implementing <see cref="IPacket"/>.</typeparam>
public interface IPacketEncryptor<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Encrypts the specified packet with a given key and algorithm.
    /// </summary>
    /// <param name="packet">The packet to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="algorithm">The symmetric algorithm to apply.</param>
    /// <returns>ENCRYPTED <typeparamref name="TPacket"/>.</returns>
    static abstract TPacket Encrypt(TPacket packet, System.Byte[] key, CipherSuiteType algorithm);

    /// <summary>
    /// Decrypts the specified packet with a given key and algorithm.
    /// </summary>
    /// <param name="packet">The packet to decrypt.</param>
    /// <param name="key">The decryption key.</param>
    /// <returns>Decrypted <typeparamref name="TPacket"/>.</returns>
    static abstract TPacket Decrypt(TPacket packet, System.Byte[] key);
}