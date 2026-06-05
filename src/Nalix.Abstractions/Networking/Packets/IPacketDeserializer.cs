// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Exposes static deserialization capability for a packet type.
/// </summary>
/// <typeparam name="TPacket">Packet type implementing <see cref="IPacket"/>.</typeparam>
public interface IPacketDeserializer<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Deserializes a packet instance from the specified byte buffer.
    /// </summary>
    /// <param name="buffer">A read-only span containing the serialized packet bytes.</param>
    /// <returns>A new <typeparamref name="TPacket"/> instance.</returns>
#if NET7_0_OR_GREATER
    static abstract TPacket Deserialize(ReadOnlySpan<byte> buffer);
#else
    TPacket Deserialize(ReadOnlySpan<byte> buffer);
#endif
}
