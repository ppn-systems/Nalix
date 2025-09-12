// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Protocols;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that checks the permission level of the current connection
/// before allowing the packet to proceed to the next handler.
/// </summary>
[PacketMiddleware(MiddlewareStage.Inbound, order: 2, name: "Permission")]
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
            System.UInt32 sequenceId = 0;
            if (context.Packet is IPacketSequenced s)
            {
                sequenceId = s.SequenceId;
            }

            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolCode.UNAUTHENTICATED,
                action: ProtocolAction.NONE,
                sequenceId: sequenceId,
                flags: ControlFlags.NONE,
                arg0: (System.Byte)context.Attributes.Permission.Level,
                arg1: (System.Byte)context.Connection.Level,
                arg2: context.Attributes.OpCode.OpCode).ConfigureAwait(false);

            return;
        }

        await next();
    }
}
