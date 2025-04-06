namespace Notio.Common.Package;

/// <summary>
/// Provides a contract for deserializing a packet of type <typeparamref name="T"/> from a span of bytes.
/// </summary>
/// <typeparam name="T">
/// The packet type that implements <see cref="IPacket"/> and supports static deserialization.
/// </typeparam>
public interface IPacketDeserializer<T> where T : IPacket
{
    /// <summary>
    /// Deserializes a packet of type <typeparamref name="T"/> from the given buffer.
    /// </summary>
    /// <param name="buffer">
    /// The read-only span of bytes containing the serialized packet data.
    /// </param>
    /// <returns>
    /// An instance of <typeparamref name="T"/> that was deserialized from the buffer.
    /// </returns>
    static abstract T Deserialize(System.ReadOnlySpan<byte> buffer);
}
