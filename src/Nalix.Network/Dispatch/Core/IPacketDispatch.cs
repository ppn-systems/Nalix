using Nalix.Common.Connection;
using Nalix.Common.Package;

namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// Defines a dispatcher interface for handling incoming network packets.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements both <see cref="IPacket"/> and <see cref="IPacketDeserializer{TPacket}"/>.
/// </typeparam>
/// <remarks>
/// Implementations of this interface are responsible for deserializing and processing
/// network packets based on their content and the connection from which they originate.
/// </remarks>
public interface IPacketDispatch<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>,
    IPacketDeserializer<TPacket>
{
    /// <summary>
    /// Handles an incoming packet represented as a <see cref="byte"/> array.
    /// </summary>
    /// <param name="packet">The byte array containing the raw packet data.</param>
    /// <param name="connection">The connection from which the packet was received.</param>
    void HandlePacket(byte[]? packet, IConnection connection);

    /// <summary>
    /// Handles an incoming packet represented as a <see cref="System.ReadOnlyMemory{Byte}"/>.
    /// </summary>
    /// <param name="packet">The memory buffer containing the raw packet data.</param>
    /// <param name="connection">The connection from which the packet was received.</param>
    void HandlePacket(System.ReadOnlyMemory<byte>? packet, IConnection connection);

    /// <summary>
    /// Handles an incoming packet represented as a <see cref="System.ReadOnlySpan{Byte}"/>.
    /// </summary>
    /// <param name="packet">The span containing the raw packet data.</param>
    /// <param name="connection">The connection from which the packet was received.</param>
    void HandlePacket(in System.ReadOnlySpan<byte> packet, IConnection connection);

    /// <summary>
    /// Handles a fully deserialized packet.
    /// </summary>
    /// <param name="packet">The deserialized packet instance to be handled.</param>
    /// <param name="connection">The connection from which the packet was received.</param>
    void HandlePacketAsync(TPacket packet, IConnection connection);
}