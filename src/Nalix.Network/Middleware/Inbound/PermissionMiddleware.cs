// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that checks the permission level of the current connection
/// before allowing the packet to proceed to the next handler.
/// </summary>
[Middleware(MiddlewareStage.Inbound, order: 2, name: "Permission")]
public class PermissionMiddleware : IPacketMiddleware<IPacket>
{
    /// <summary>
    /// Invokes the permission check logic. If the connection's permission level is
    /// lower than required by the packet attributes, an error message is sent and processing stops.
    /// </summary>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next,
        System.Threading.CancellationToken ct)
    {
        if (context.Attributes.Permission is not null &&
            context.Attributes.Permission.Level > context.Connection.Level)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(PermissionMiddleware)}] deny op=0x{context.Attributes.OpCode.OpCode:X} " +
                                          $"need={context.Attributes.Permission.Level} have={context.Connection.Level}");

            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolCode.UNAUTHENTICATED,
                action: ProtocolAction.NONE,
                sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                flags: ControlFlags.NONE,
                arg0: (System.Byte)context.Attributes.Permission.Level,
                arg1: (System.Byte)context.Connection.Level,
                arg2: context.Attributes.OpCode.OpCode).ConfigureAwait(false);

            return;
        }

        await next(ct);
    }
}
