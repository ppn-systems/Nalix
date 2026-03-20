// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Pipeline.Throttling;

namespace Nalix.Network.Pipeline.Inbound;

/// <summary>
/// Middleware that enforces concurrency limits on incoming packets.
/// </summary>
[MiddlewareOrder(50)] // Execute after security checks
[MiddlewareStage(MiddlewareStage.Inbound)]
public class ConcurrencyMiddleware : IPacketMiddleware<IPacket>
{
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
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
                    Directive directive = s_pool.Get<Directive>();

                    try
                    {
                        directive.Initialize(
                            ControlType.FAIL,
                            ProtocolReason.RATE_LIMITED, ProtocolAdvice.RETRY,
                            sequenceId: context.Packet.SequenceId,
                            flags: ControlFlags.IS_TRANSIENT,
                            arg0: context.Packet.OpCode);

                        using BufferLease lease_1 = BufferLease.Rent(directive.Length + 32);

                        int length = directive.Serialize(lease_1.SpanFull);
                        lease_1.CommitLength(length);
                        await context.Connection.TCP.SendAsync(lease_1.Memory).ConfigureAwait(false);
                    }
                    finally
                    {
                        s_pool.Return(directive);
                    }

                    return;
                }
            }

            await next(context.CancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyFailureException)
        {
            Directive directive = s_pool.Get<Directive>();

            try
            {
                directive.Initialize(
                    ControlType.FAIL,
                    ProtocolReason.RATE_LIMITED, ProtocolAdvice.RETRY,
                    sequenceId: context.Packet.SequenceId,
                    flags: ControlFlags.IS_TRANSIENT,
                    arg0: context.Packet.OpCode);

                using BufferLease lease_1 = BufferLease.Rent(directive.Length + 32);

                int length = directive.Serialize(lease_1.SpanFull);
                lease_1.CommitLength(length);
                await context.Connection.TCP.SendAsync(lease_1.Memory).ConfigureAwait(false);
            }
            finally
            {
                s_pool.Return(directive);
            }
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
