// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Dispatch.Abstractions;
using Nalix.Network.Dispatch.Attributes;
using Nalix.Network.Dispatch.Enums;

namespace Nalix.Network.Dispatch.Middleware.Outbound;

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
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        if (context.Attributes.Permission is not null &&
            context.Attributes.Permission.Level > context.Connection.Level)
        {
            _ = await context.Connection.Tcp.SendAsync("Permission denied. You are not authorized to perform this action.")
                                            .ConfigureAwait(false);
        }

        await next();
    }
}
