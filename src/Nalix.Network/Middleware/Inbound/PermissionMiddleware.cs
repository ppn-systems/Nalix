// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connections;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that checks the permission level of the current connection
/// before allowing the packet to proceed to the next handler.
/// </summary>
public class PermissionMiddleware : IPacketMiddleware<IPacket>
{
    /// <summary>
    /// Invokes the concurrency middleware, enforcing concurrency limits on incoming packets.
    /// </summary>
    /// <param name="context">The packet context containing the packet and connection information.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        if (context.Attributes.Permission is not null &&
            context.Attributes.Permission.Level > context.Connection.Level)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[NW.{nameof(PermissionMiddleware)}] deny op=0x{context.Attributes.OpCode.OpCode:X} " +
                                           $"need={context.Attributes.Permission.Level} have={context.Connection.Level}");

            System.UInt32 sequenceId = context.Packet is IPacketSequenced sequenced
                ? sequenced.SequenceId
                : 0;

            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.UNAUTHENTICATED,
                action: ProtocolAdvice.NONE,
                sequenceId: sequenceId,
                flags: ControlFlags.NONE,
                arg0: (System.Byte)context.Attributes.Permission.Level,
                arg1: (System.Byte)context.Connection.Level, arg2: context.Attributes.OpCode.OpCode).ConfigureAwait(false);

            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}

