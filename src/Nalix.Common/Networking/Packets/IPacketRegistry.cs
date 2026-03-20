// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking.Packets;

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
/// </list>
/// Implementations must be safe for concurrent read access if used across threads.
/// </remarks>
public interface IPacketRegistry
{
    /// <summary>
    /// Gets the number of deserializers registered in this catalog.
    /// </summary>
    System.Int32 DeserializerCount { get; }

    /// <summary>
    /// Returns <see langword="true"/> if a deserializer is registered for
    /// <paramref name="magic"/>.
    /// </summary>
    System.Boolean IsKnownMagic(System.UInt32 magic);

    /// <summary>
    /// Returns <see langword="true"/> if a deserializer is registered for the packet type
    /// <typeparamref name="TPacket"/>, resolved via its FNV-1a magic number.
    /// </summary>
    System.Boolean IsRegistered<TPacket>() where TPacket : IPacket;

    /// <summary>
    /// Attempts to deserialize a packet by reading the magic number from the provided
    /// raw buffer and dispatching to the registered deserializer.
    /// </summary>
    /// <param name="raw">
    /// The raw byte span. The first four bytes are interpreted as a little-endian
    /// 32-bit magic number. Must be at least <see cref="PacketConstants.HeaderSize"/> bytes.
    /// </param>
    /// <param name="packet">
    /// When this method returns <see langword="true"/>, the deserialized
    /// <see cref="IPacket"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; <see langword="false"/> when the buffer is
    /// too short or no deserializer is registered for the magic number found.
    /// </returns>
    System.Boolean TryDeserialize(System.ReadOnlySpan<System.Byte> raw, out IPacket packet);

    /// <summary>
    /// Attempts to get the <see cref="PacketDeserializer"/> associated with the specified magic number.
    /// </summary>
    /// <param name="magic">The 32-bit magic number.</param>
    /// <param name="deserializer">
    /// When this method returns <see langword="true"/>, the matching delegate;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a deserializer is registered; otherwise <see langword="false"/>.
    /// </returns>
    System.Boolean TryGetDeserializer(System.UInt32 magic, out PacketDeserializer deserializer);
}
