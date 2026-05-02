// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Provides a read-only lookup catalog for packet deserializers.
/// </summary>
/// <remarks>
/// Implementations should support concurrent reads because the registry is used by
/// the packet dispatch path.
/// </remarks>
public interface IPacketRegistry
{
    /// <summary>
    /// Gets the number of registered deserializers.
    /// </summary>
    int DeserializerCount { get; }

    /// <summary>
    /// Returns <see langword="true"/> if a deserializer is registered for the magic number.
    /// </summary>
    bool IsKnownMagic(uint magic);

    /// <summary>
    /// Returns <see langword="true"/> if a deserializer is registered for <typeparamref name="TPacket"/>.
    /// </summary>
    bool IsRegistered<TPacket>() where TPacket : IPacket;

    /// <summary>
    /// Attempts to deserialize a packet by resolving the magic number from the raw buffer
    /// and dispatching to the matching deserializer.
    /// </summary>
    /// <param name="raw">
    /// The raw byte span. The first four bytes are interpreted as a little-endian
    /// 32-bit magic number.
    /// </param>
    /// <returns>
    /// The deserialized packet.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when a registered deserializer rejects a malformed packet buffer.</exception>
    IPacket Deserialize(ReadOnlySpan<byte> raw);

    /// <summary>
    /// Attempts to deserialize a packet without throwing for unknown magic or short input.
    /// </summary>
    /// <param name="raw">Raw incoming packet bytes.</param>
    /// <param name="packet">The resolved packet when successful.</param>
    /// <returns><see langword="true"/> when deserialization succeeds.</returns>
    bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet);

    /// <summary>
    /// Attempts to read only the packet header from the raw buffer without full deserialization.
    /// This enables raw handler dispatch where the handler receives the original bytes.
    /// </summary>
    /// <param name="raw">Raw incoming packet bytes (must be at least <see cref="PacketConstants.HeaderSize"/> bytes).</param>
    /// <param name="header">When this method returns <see langword="true"/>, contains the parsed header.</param>
    /// <returns><see langword="true"/> when the header was successfully read.</returns>
    bool TryReadHeader(ReadOnlySpan<byte> raw, out Primitives.PacketHeader header);
}
