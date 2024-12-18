// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Dispatch.Core.Context;
using Nalix.Network.Dispatch.Middleware.Core.Attributes;
using Nalix.Network.Dispatch.Middleware.Core.Enums;
using Nalix.Network.Dispatch.Middleware.Core.Interfaces;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Text;

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
            Text256 text = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                   .Get<Text256>();
            try
            {
                text.Initialize("Permission denied. You are not authorized to perform this action.");
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());
                return;
            }
            finally
            {
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return(text);
            }
        }

        await next();
    }
}
