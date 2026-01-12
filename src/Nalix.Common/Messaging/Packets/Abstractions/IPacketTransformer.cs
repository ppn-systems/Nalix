// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Messaging.Packets.Abstractions;

/// <summary>
/// Defines the static transformation contract for a packet type, including
/// serialization, encryption/decryption, and compression/decompression.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements <see cref="IPacket"/> and provides its own static transformation methods.
/// </typeparam>
public interface IPacketTransformer<TPacket> :
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>,
    IPacketDeserializer<TPacket> where TPacket : IPacket
{
    // Intentionally empty: acts as a convenience aggregator.
}
