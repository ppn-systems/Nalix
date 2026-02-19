// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly ConcurrencyGate _concurrencyGate;

    /// <inheritdoc/>
    public ConcurrencyMiddleware() => _concurrencyGate = InstanceManager.Instance.GetOrCreateInstance<ConcurrencyGate>();

    /// <inheritdoc/>
    public ConcurrencyMiddleware(ConcurrencyGate concurrencyGate) => _concurrencyGate = concurrencyGate ?? throw new ArgumentNullException(nameof(concurrencyGate));

    /// <summary>
    /// Invokes the concurrency middleware, enforcing concurrency limits on incoming packets.
    /// </summary>
    /// <param name="context">The packet context containing the packet and connection information.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask InvokeAsync(PacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);

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
                lease = await _concurrencyGate.EnterAsync(context.Packet.OpCode, context.Attributes.ConcurrencyLimit, context.CancellationToken)
                                              .ConfigureAwait(false);
                acquired = true;
            }
            else
            {
                acquired = _concurrencyGate.TryEnter(context.Packet.OpCode, context.Attributes.ConcurrencyLimit, out lease);

                if (!acquired)
                {
                    await context.Connection.SendAsync(
                        controlType: ControlType.FAIL,
                        reason: ProtocolReason.RATE_LIMITED,
                        action: ProtocolAdvice.RETRY,
                        options: new ControlDirectiveOptions(Flags: ControlFlags.IS_TRANSIENT, SequenceId: context.Packet.SequenceId, Arg0: context.Packet.OpCode)).ConfigureAwait(false);

                    return;
                }
            }

            await next(context.CancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyFailureException)
        {
            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.RATE_LIMITED,
                action: ProtocolAdvice.RETRY,
                options: new ControlDirectiveOptions(Flags: ControlFlags.IS_TRANSIENT, SequenceId: context.Packet.SequenceId, Arg0: context.Packet.OpCode)).ConfigureAwait(false);
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
