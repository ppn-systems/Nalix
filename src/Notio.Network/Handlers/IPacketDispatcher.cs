using Notio.Common;
using Notio.Common.Connection;

namespace Notio.Network.Handlers;

/// <summary>
/// Defines a dispatcher interface for handling packets.
/// </summary>
public interface IPacketDispatcher
{
    /// <summary>
    /// Handles the incoming packet and processes it with the specified connection.
    /// </summary>
    /// <param name="packet">The packet to handle.</param>
    /// <param name="connection">The connection associated with the packet.</param>
    void HandlePacket(IPacket packet, IConnection connection);
}
