namespace Notio.Network.Dispatcher;

/// <summary>
/// Defines a dispatcher interface for handling incoming network packets.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for processing incoming packets
/// and handling them appropriately based on their content and associated connection.
/// </remarks>
public interface IPacketDispatcher<TPacket>
    where TPacket : Common.Package.IPacket, Common.Package.IPacketDeserializer<TPacket>
{
    /// <summary>
    /// Handles the incoming byte array packet and processes it using the specified connection.
    /// </summary>
    /// <param name="packet">The byte array representing the received packet to be processed.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// Implementations should deserialize the packet and then determine the appropriate action
    /// based on the packet's content and the associated command Number.
    /// </remarks>
    void HandlePacket(byte[]? packet, Common.Connection.IConnection connection);

    /// <summary>
    /// Handles the incoming byte array packet and processes it using the specified connection.
    /// </summary>
    /// <param name="packet">The byte array representing the received packet to be processed.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// Implementations should deserialize the packet and then determine the appropriate action
    /// based on the packet's content and the associated command Number.
    /// </remarks>
    void HandlePacket(System.ReadOnlyMemory<byte>? packet, Common.Connection.IConnection connection);

    /// <summary>
    /// Handles the incoming byte array packet and processes it using the specified connection.
    /// </summary>
    /// <param name="packet">The byte array representing the received packet to be processed.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// Implementations should deserialize the packet and then determine the appropriate action
    /// based on the packet's content and the associated command Number.
    /// </remarks>
    void HandlePacket(in System.ReadOnlySpan<byte> packet, Common.Connection.IConnection connection);

    /// <summary>
    /// Handles the incoming packet and processes it using the specified connection.
    /// </summary>
    /// <param name="packet">The received packet to be handled.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// Implementations should determine the appropriate action based on the packet's command Number
    /// and perform the necessary processing using the provided connection.
    /// </remarks>
    void HandlePacketAsync(TPacket packet, Common.Connection.IConnection connection);
}
