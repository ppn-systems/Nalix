// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Routing;
using Nalix.Network.Throttling;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that enforces concurrency limits on incoming packets.
/// </summary>
[MiddlewareOrder(50)] // Execute after security checks
[MiddlewareStage(MiddlewareStage.Inbound)]
public class ConcurrencyMiddleware : IPacketMiddleware<IPacket>
{
    private readonly ConcurrencyGate s_ConcurrencyGate = InstanceManager.Instance.GetOrCreateInstance<ConcurrencyGate>();

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

        bool acquired = false;
        ConcurrencyGate.Lease lease = default;

        try
        {
            if (context.Attributes.ConcurrencyLimit.Queue)
            {
                lease = await s_ConcurrencyGate.EnterAsync(context.Packet.OpCode, context.Attributes.ConcurrencyLimit, context.CancellationToken)
                                              .ConfigureAwait(false);
                acquired = true;
            }
            else
            {
                acquired = s_ConcurrencyGate.TryEnter(context.Packet.OpCode, context.Attributes.ConcurrencyLimit, out lease);

                if (!acquired)
                {
                    uint sequenceId1 = context.Packet is IPacketSequenced sequenced1
                        ? sequenced1.SequenceId
                        : 0;

                    await context.Connection.SendAsync(
                        controlType: ControlType.FAIL,
                        reason: ProtocolReason.RATE_LIMITED,
                        action: ProtocolAdvice.RETRY,
                        sequenceId: sequenceId1,
                        flags: ControlFlags.IS_TRANSIENT,
                        arg0: context.Packet.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);

                    return;
                }
            }

            await next(context.CancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            uint sequenceId2 = context.Packet is IPacketSequenced sequenced2
                ? sequenced2.SequenceId
                : 0;

            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.RATE_LIMITED,
                action: ProtocolAdvice.RETRY,
                sequenceId: sequenceId2,
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
