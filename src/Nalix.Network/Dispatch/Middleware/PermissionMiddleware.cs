using Nalix.Common.Packets;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;

namespace Nalix.Network.Dispatch.Middleware;

/// <summary>
/// Middleware that checks the permission level of the current connection
/// before allowing the packet to proceed to the next handler.
/// </summary>
/// <typeparam name="TPacket">The packet type being processed.</typeparam>
public class PermissionMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketFactory<TPacket>
{
    /// <summary>
    /// Invokes the permission check logic. If the connection's permission level is
    /// lower than required by the packet attributes, an error message is sent and processing stops.
    /// </summary>
    /// <param name="context">The packet context containing the connection and attributes.</param>
    /// <param name="next">The next middleware delegate to invoke.</param>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        if (context.Attributes.Permission is not null &&
            context.Attributes.Permission.Level > context.Connection.Level)
        {
            _ = await context.Connection.Tcp.SendAsync(TPacket
                                        .Create(0, "Permission denied. You are not authorized to perform this action."));

            return;
        }

        await next();
    }
}
