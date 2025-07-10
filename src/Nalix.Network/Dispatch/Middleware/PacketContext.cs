using Nalix.Common.Connection;

namespace Nalix.Network.Dispatch.Middleware;

/// <summary>
/// Represents the context for a received packet, containing both the packet itself
/// and the connection through which it was received.
/// </summary>
/// <typeparam name="TPacket">The type of the packet.</typeparam>
public class PacketContext<TPacket>
{
    /// <summary>
    /// Gets or sets the packet being processed.
    /// </summary>
    public TPacket Packet { get; set; }

    /// <summary>
    /// Gets or sets the connection through which the packet was received.
    /// </summary>
    public IConnection Connection { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketContext{TPacket}"/> class.
    /// </summary>
    /// <param name="packet">The packet being processed.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public PacketContext(TPacket packet, IConnection connection)
    {
        Packet = packet;
        Connection = connection;
    }
}