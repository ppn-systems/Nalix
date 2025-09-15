// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware;

/// <summary>
/// Represents a middleware pipeline for processing packets.
/// Allows chaining multiple middleware components to handle a packet context.
/// </summary>
/// <typeparam name="TPacket">The type of packet being processed in the pipeline.</typeparam>
public class MiddlewarePipeline<TPacket>
{
    private readonly System.Collections.Generic.List<IPacketMiddleware<TPacket>> _inbound = [];
    private readonly System.Collections.Generic.List<IPacketMiddleware<TPacket>> _outbound = [];
    private readonly System.Collections.Generic.List<IPacketMiddleware<TPacket>> _outboundAlways = [];

    /// <summary>
    /// Adds a middleware component to be executed before the main handler.
    /// </summary>
    public void UseInbound(IPacketMiddleware<TPacket> middleware) => _inbound.Add(middleware);

    /// <summary>
    /// Adds a middleware component to be executed after the main handler.
    /// </summary>
    public void UseOutbound(IPacketMiddleware<TPacket> middleware) => _outbound.Add(middleware);

    /// <summary>
    /// Adds a middleware component to be executed after the main handler, regardless of outbound skipping.
    /// </summary>
    public void UseOutboundAlways(IPacketMiddleware<TPacket> m) => _outboundAlways.Add(m);

    /// <summary>
    /// Executes the pipeline asynchronously using the provided packet context and terminal handler.
    /// Middlewares are invoked in the order they were added, forming a chain of responsibility.
    /// </summary>
    public System.Threading.Tasks.Task ExecuteAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> handler,
        System.Threading.CancellationToken ct = default)
    {
        // Build inbound chain → handler → outbound chain
        return ExecuteMiddlewareChain(
            _inbound, context,
            async (downstreamCt) =>
            {
                // Call terminal handler
                await handler(downstreamCt).ConfigureAwait(false);

                await ExecuteMiddlewareChain(
                    _outboundAlways, context,
                    _ => System.Threading.Tasks.Task.CompletedTask, downstreamCt
                ).ConfigureAwait(false);

                // Then run outbound chain (separately) with the same downstream token
                if (!context.SkipOutbound)
                {
                    await ExecuteMiddlewareChain(
                        _outbound, context,
                        _ => System.Threading.Tasks.Task.CompletedTask, downstreamCt
                    ).ConfigureAwait(false);
                }
            },
            ct
        );
    }

    // -------------------- Core chain builder (token-aware) --------------------

    private static System.Threading.Tasks.Task ExecuteMiddlewareChain(
        System.Collections.Generic.List<IPacketMiddleware<TPacket>> middlewares,
        PacketContext<TPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> final,
        System.Threading.CancellationToken ct)
    {
        // Start from 'final', wrap backwards.
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next = final;

        for (System.Int32 i = middlewares.Count - 1; i >= 0; i--)
        {
            IPacketMiddleware<TPacket> current = middlewares[i];
            var localNext = next;

            // Each middleware receives the upstream token 'ct' as parameter,
            // and may choose to pass the SAME or a NEW token to localNext(...)
            next = (upstreamCt) => current.InvokeAsync(context, localNext, upstreamCt);
        }

        // Kick off the chain with the provided 'ct'
        return next(ct);
    }
}
