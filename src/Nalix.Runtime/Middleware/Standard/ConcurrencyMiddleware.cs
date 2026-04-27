// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Framework.Injection;
using Nalix.Runtime.Internal.RateLimiting;
using Nalix.Runtime.Pooling;
using Nalix.Runtime.Throttling;

namespace Nalix.Runtime.Middleware.Standard;

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
    public async ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
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
                    if (!DirectiveGuard.TryAcquire(
                        context.Connection,
                        ConnectionAttributes.InboundDirectiveRateLimitedLastSentAtMs))
                    {
                        return;
                    }

                    using PacketLease<Directive> packetLease = PacketPool<Directive>.Rent();
                    Directive directive = packetLease.Value;

                    directive.Initialize(
                        ControlType.FAIL,
                        ProtocolReason.RATE_LIMITED, ProtocolAdvice.RETRY,
                        sequenceId: context.Packet.SequenceId,
                        controlFlags: ControlFlags.IS_TRANSIENT,
                        arg0: context.Packet.OpCode);

                    await context.Sender.SendAsync(directive).ConfigureAwait(false);

                    return;
                }
            }

            await next(context.CancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyFailureException)
        {
            if (!DirectiveGuard.TryAcquire(
                context.Connection,
                ConnectionAttributes.InboundDirectiveRateLimitedLastSentAtMs))
            {
                return;
            }

            using PacketLease<Directive> packetLease = PacketPool<Directive>.Rent();
            Directive directive = packetLease.Value;

            directive.Initialize(
                ControlType.FAIL,
                ProtocolReason.RATE_LIMITED, ProtocolAdvice.RETRY,
                sequenceId: context.Packet.SequenceId,
                controlFlags: ControlFlags.IS_TRANSIENT,
                arg0: context.Packet.OpCode);

            await context.Sender.SendAsync(directive).ConfigureAwait(false);
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
