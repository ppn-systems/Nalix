// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Messaging.Packets.Abstractions;

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
    static abstract TPacket Deserialize(System.ReadOnlySpan<System.Byte> buffer);

    // (Optional) If your design needs it, you can add the opposite direction:
    // static abstract void Serialize(in TPacket packet, SYSTEM.Span<byte> destination);
}