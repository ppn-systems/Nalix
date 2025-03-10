using Notio.Common.Connection;
using Notio.Common.Interfaces;
using System;

namespace Notio.Network.Handlers;

/// <summary>
/// Defines a dispatcher interface for handling incoming network packets.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for processing incoming packets
/// and handling them appropriately based on their content and associated connection.
/// </remarks>
public interface IPacketDispatcher
{
    /// <summary>
    /// Handles the incoming byte array packet and processes it using the specified connection.
    /// </summary>
    /// <param name="packet">The byte array representing the received packet to be processed.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// Implementations should deserialize the packet and then determine the appropriate action
    /// based on the packet's content and the associated command ID.
    /// </remarks>
    void HandlePacket(byte[]? packet, IConnection connection);

    /// <summary>
    /// Handles the incoming byte array packet and processes it using the specified connection.
    /// </summary>
    /// <param name="packet">The byte array representing the received packet to be processed.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// Implementations should deserialize the packet and then determine the appropriate action
    /// based on the packet's content and the associated command ID.
    /// </remarks>
    void HandlePacket(ReadOnlyMemory<byte>? packet, IConnection connection);

    /// <summary>
    /// Handles the incoming packet and processes it using the specified connection.
    /// </summary>
    /// <param name="packet">The received packet to be handled.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// Implementations should determine the appropriate action based on the packet's command ID
    /// and perform the necessary processing using the provided connection.
    /// </remarks>
    void HandlePacket(IPacket? packet, IConnection connection);
}
