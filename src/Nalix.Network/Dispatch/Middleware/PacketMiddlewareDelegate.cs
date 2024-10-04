using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.Middleware;

/// <summary>
/// Delegate type representing a middleware handler for processing packets.
/// It receives the packet, the connection through which the packet was received,
/// and a delegate to invoke the next middleware in the pipeline.
/// </summary>
/// <typeparam name="TPacket">The type of packet being processed.</typeparam>
/// <param name="context">Encapsulates the packet and its connection metadata.</param>
/// <param name="next">Delegate to invoke the next middleware component.</param>
public delegate System.Threading.Tasks.Task PacketMiddlewareDelegate<TPacket>(
    PacketContext<TPacket> context,
    System.Func<System.Threading.Tasks.Task> next);