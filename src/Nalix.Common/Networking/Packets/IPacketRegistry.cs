// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Nalix.Common.Networking.Packets;

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
    /// Attempts to deserialize a packet into an existing typed packet value.
    /// Implementations may reuse or replace <paramref name="value"/> depending on concrete packet behavior.
    /// </summary>
    /// <typeparam name="TPacket">Expected packet type.</typeparam>
    /// <param name="raw">Raw incoming packet bytes.</param>
    /// <param name="value">Existing packet value to populate or replace.</param>
    /// <returns>The deserialized packet.</returns>
    /// <exception cref="ArgumentException">Thrown when a registered deserializer rejects a malformed packet buffer.</exception>
    /// <exception cref="InvalidOperationException">Thrown when deserialized type does not match <typeparamref name="TPacket"/>.</exception>
    TPacket Deserialize<TPacket>(ReadOnlySpan<byte> raw, ref TPacket value) where TPacket : IPacket
    {
        IPacket packet = this.Deserialize(raw);
        if (packet is not TPacket typed)
        {
            throw new InvalidOperationException(
                $"Deserialized packet type mismatch. Expected '{typeof(TPacket).FullName}', actual '{packet.GetType().FullName}'.");
        }

        value = typed;
        return typed;
    }

    /// <summary>
    /// Attempts to deserialize a packet without throwing for unknown magic or short input.
    /// </summary>
    /// <param name="raw">Raw incoming packet bytes.</param>
    /// <param name="packet">The resolved packet when successful.</param>
    /// <returns><see langword="true"/> when deserialization succeeds.</returns>
    bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet);

    /// <summary>
    /// Attempts to deserialize a packet into an existing typed packet value without throwing for unknown magic or short input.
    /// </summary>
    /// <typeparam name="TPacket">Expected packet type.</typeparam>
    /// <param name="raw">Raw incoming packet bytes.</param>
    /// <param name="value">Existing packet value to populate or replace.</param>
    /// <returns><see langword="true"/> when deserialization succeeds and type matches <typeparamref name="TPacket"/>.</returns>
    bool TryDeserialize<TPacket>(ReadOnlySpan<byte> raw, ref TPacket value) where TPacket : IPacket
    {
        bool ok = this.TryDeserialize(raw, out IPacket? packet);
        if (!ok || packet is not TPacket typed)
        {
            return false;
        }

        value = typed;
        return true;
    }

}
