using Notio.Common.Connection;
using Notio.Common.Package;

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
    /// Handles the incoming packet and processes it using the specified connection.
    /// </summary>
    /// <param name="packet">The received packet to be handled.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// Implementations should determine the appropriate action based on the packet type
    /// and its associated command ID.
    /// </remarks>
    void HandlePacket(IPacket packet, IConnection connection);
}
