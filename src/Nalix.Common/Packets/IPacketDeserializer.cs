namespace Nalix.Common.Packets;

/// <summary>
/// Provides a contract for deserializing a packet of type <typeparamref name="TPacket"/> from a span of bytes.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements <see cref="IPacket"/> and supports static deserialization.
/// </typeparam>
public interface IPacketDeserializer<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Deserializes a packet of type <typeparamref name="TPacket"/> from the given buffer.
    /// </summary>
    /// <param name="buffer">
    /// The read-only span of bytes containing the serialized packet data.
    /// </param>
    /// <returns>
    /// An instance of <typeparamref name="TPacket"/> that was deserialized from the buffer.
    /// </returns>
    static abstract TPacket Deserialize(System.ReadOnlySpan<System.Byte> buffer);
}
