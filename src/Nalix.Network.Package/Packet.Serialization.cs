using Nalix.Common.Exceptions;
using Nalix.Common.Package;
using Nalix.Network.Package.Engine.Serialization;

namespace Nalix.Network.Package;

public readonly partial struct Packet : IPacketDeserializer<Packet>
{
    /// <summary>
    /// Serializes the packet into a new byte array.
    /// </summary>
    /// <returns>
    /// A byte array containing the serialized representation of the packet.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Memory<byte> Serialize() => PacketSerializer.Serialize(this);

    /// <summary>
    /// Serializes the packet into the provided buffer.
    /// </summary>
    /// <param name="buffer">
    /// A span of bytes to write the serialized packet into. The buffer must be large enough to hold the entire packet.
    /// </param>
    /// <exception cref="PackageException">
    /// Thrown if the buffer is too small to contain the serialized packet.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(System.Span<byte> buffer)
        => PacketSerializer.WritePacketUnsafe(buffer, this);

    /// <summary>
    /// Deserializes a <see cref="Packet"/> from the given byte buffer using fast deserialization logic.
    /// </summary>
    /// <param name="buffer">
    /// A read-only span of bytes that contains the serialized data of the packet.
    /// </param>
    /// <returns>
    /// A <see cref="Packet"/> instance reconstructed from the given buffer.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketDeserializer<Packet>.Deserialize(System.ReadOnlySpan<byte> buffer)
        => PacketSerializer.ReadPacket(buffer);
}
