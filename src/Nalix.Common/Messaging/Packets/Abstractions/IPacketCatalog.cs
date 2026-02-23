// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Messaging.Packets.Abstractions;

/// <summary>
/// Provides a read-only catalog that maps magic numbers and packet types
/// to their corresponding deserializers and transformation delegates.
/// </summary>
/// <remarks>
/// The catalog exposes lookup methods for:
/// <list type="bullet">
///   <item>
///     <description>Deserializing a packet by inspecting its magic number.</description>
///   </item>
///   <item>
///     <description>Retrieving a typed <see cref="PacketDeserializer"/> by magic number.</description>
///   </item>
///   <item>
///     <description>Retrieving a <see cref="PacketTransformer"/> for a given packet <see cref="System.Type"/>.</description>
///   </item>
/// </list>
/// Implementations must be safe for concurrent read access if used across threads.
/// </remarks>
public interface IPacketCatalog
{
    /// <summary>
    /// Attempts to deserialize a packet by reading the magic number from the provided raw buffer.
    /// </summary>
    /// <param name="raw">
    /// The raw byte span that contains the serialized packet, beginning with a recognized magic number.
    /// </param>
    /// <param name="packet">
    /// When this method returns <see langword="true"/>, contains the deserialized <see cref="IPacket"/> instance;
    /// otherwise, contains <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a matching deserializer is found and deserialization succeeds; otherwise, <see langword="false"/>.
    /// </returns>
    System.Boolean TryDeserialize(System.ReadOnlySpan<System.Byte> raw, out IPacket packet);

    /// <summary>
    /// Attempts to get a packet deserializer associated with the specified magic number.
    /// </summary>
    /// <param name="magic">
    /// The 32-bit magic number that identifies a packet format.
    /// </param>
    /// <param name="deserializer">
    /// When this method returns <see langword="true"/>, contains the <see cref="PacketDeserializer"/> for the magic number;
    /// otherwise, contains <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a deserializer is registered for the given magic number; otherwise, <see langword="false"/>.
    /// </returns>
    System.Boolean TryGetDeserializer(System.UInt32 magic, out PacketDeserializer deserializer);

    /// <summary>
    /// Attempts to get the transformer delegates associated with the specified packet type.
    /// </summary>
    /// <param name="packetType">
    /// The concrete <see cref="System.Type"/> of the packet for which transformers are requested.
    /// </param>
    /// <param name="transformer">
    /// When this method returns <see langword="true"/>, contains the <see cref="PacketTransformer"/> configured for the type;
    /// otherwise, contains the default value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if transformers are registered for the specified packet type; otherwise, <see langword="false"/>.
    /// </returns>
    System.Boolean TryGetTransformer(System.Type packetType, out PacketTransformer transformer);
}
