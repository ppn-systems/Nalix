using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;
using Nalix.Network.Dispatch.Middleware.Enums;
using Nalix.Network.Dispatch.Middleware.Interfaces;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging;
using System;
using System.Threading.Tasks;

namespace Nalix.Network.Dispatch.Middleware.Pre;

/// <summary>
/// Middleware that checks the permission level of the current connection
/// before allowing the packet to proceed to the next handler.
/// </summary>
[PacketMiddleware(MiddlewareStage.PreDispatch, order: 2, name: "Permission")]
public class PermissionMiddleware : IPacketMiddleware<IPacket>
{
    /// <summary>
    /// Invokes the permission check logic. If the connection's permission level is
    /// lower than required by the packet attributes, an error message is sent and processing stops.
    /// </summary>
    public async Task InvokeAsync(
        PacketContext<IPacket> context,
        Func<Task> next)
    {
        if (context.Attributes.Permission is not null &&
            context.Attributes.Permission.Level > context.Connection.Level)
        {
            var text = ObjectPoolManager.Instance.Get<TextPacket>();
            try
            {
                text.Initialize("Permission denied. You are not authorized to perform this action.");
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());
                return;
            }
            finally
            {
                ObjectPoolManager.Instance.Return(text);
            }
        }

        await next();
    }
}
