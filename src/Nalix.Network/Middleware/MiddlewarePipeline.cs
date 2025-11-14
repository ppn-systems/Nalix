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
    /// Gets a value indicating whether the middleware pipeline contains no middleware components.
    /// </summary>
    public System.Boolean IsEmpty =>
        _inbound.Count == 0 &&
        _outbound.Count == 0 &&
        _outboundAlways.Count == 0;

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
    public void UseOutboundAlways(IPacketMiddleware<TPacket> middleware) => _outboundAlways.Add(middleware);

    /// <summary>
    /// Executes the pipeline asynchronously using the provided packet context and terminal handler.
    /// Middlewares are invoked in the order they were added, forming a chain of responsibility.
    /// </summary>
    public System.Threading.Tasks.Task ExecuteAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> handler,
        System.Threading.CancellationToken ct = default)
    {
        // Build inbound chain → handler → outboundAlways → outbound
        return ExecuteMiddlewareChain(
            _inbound, context,
            async (downstreamCt) =>
            {
                // Call terminal handler
                await handler(downstreamCt).ConfigureAwait(false);

                // Run 'always outbound' with the SAME downstream token (not context.CancellationToken)
                await ExecuteMiddlewareChain(
                    _outboundAlways, context,
                    _ => System.Threading.Tasks.Task.CompletedTask,
                    downstreamCt
                ).ConfigureAwait(false);

                // Then run outbound (conditionally) with the SAME downstream token
                if (!context.SkipOutbound)
                {
                    await ExecuteMiddlewareChain(
                        _outbound, context,
                        _ => System.Threading.Tasks.Task.CompletedTask,
                        downstreamCt
                    ).ConfigureAwait(false);
                }
            },
            // Start inbound with the caller-supplied token (ct),
            // not necessarily context.CancellationToken.
            ct
        );
    }

    // -------------------- Core chain builder (token-aware) --------------------

    /// <summary>
    /// Builds and executes a middleware chain. Each middleware receives a 'next' delegate that
    /// accepts a <see cref="System.Threading.CancellationToken"/> so the middleware can either:
    ///  - pass through the current token, or
    ///  - substitute a derived/linked token.
    /// </summary>
    private static System.Threading.Tasks.Task ExecuteMiddlewareChain(
        System.Collections.Generic.List<IPacketMiddleware<TPacket>> middlewares,
        PacketContext<TPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> final,
        System.Threading.CancellationToken startToken)
    {

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
        static System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> CreateWrapper(
            PacketContext<TPacket> context, IPacketMiddleware<TPacket> middleware,
            System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
        {
            return token =>
                middleware.InvokeAsync(
                    context,
                    downstreamToken => next(downstreamToken)
                );
        }

        // Start from 'final', wrap backwards.
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next = final;

        for (System.Int32 i = middlewares.Count - 1; i >= 0; i--)
        {
            IPacketMiddleware<TPacket> current = middlewares[i];
            System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> localNext = next;
            next = CreateWrapper(context, current, localNext);
        }

        // Kick off the chain with the provided starting token
        return next(startToken);
    }
}
