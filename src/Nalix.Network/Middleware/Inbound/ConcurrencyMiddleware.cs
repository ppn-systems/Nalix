// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch;
using Nalix.Network.Throttling;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that enforces concurrency limits on incoming packets.
/// </summary>
public class ConcurrencyMiddleware : IPacketMiddleware<IPacket>
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
        if (context.Attributes.ConcurrencyLimit is null)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        System.Boolean acquired = false;
        ConcurrencyGate.Lease lease = default;

        try
        {
            if (context.Attributes.ConcurrencyLimit.Queue)
            {
                lease = await ConcurrencyGate.EnterAsync(context.Packet.OpCode, context.Attributes.ConcurrencyLimit, context.CancellationToken)
                                             .ConfigureAwait(false);
                acquired = true;
            }
            else
            {
                acquired = ConcurrencyGate.TryEnter(context.Packet.OpCode, context.Attributes.ConcurrencyLimit, out lease);

                if (!acquired)
                {
                    await context.Connection.SendAsync(
                        controlType: ControlType.FAIL,
                        reason: ProtocolCode.RATE_LIMITED,
                        action: ProtocolAction.RETRY,
                        sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                        flags: ControlFlags.IS_TRANSIENT,
                        arg0: context.Packet.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);
                    return;
                }
            }

            await next(context.CancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyRejectedException)
        {
            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolCode.RATE_LIMITED,
                action: ProtocolAction.RETRY,
                sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                flags: ControlFlags.IS_TRANSIENT,
                arg0: context.Packet.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);
        }
        finally
        {
            if (acquired)
            {
                lease.Dispose();
            }
        }
    }
}
