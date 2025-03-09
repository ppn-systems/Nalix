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

    /// <summary>
    /// Adds a middleware component to be executed before the main handler.
    /// </summary>
    /// <param name="middleware">The middleware to be added.</param>
    /// <returns>The current pipeline instance for chaining.</returns>
    public void UseInbound(IPacketMiddleware<TPacket> middleware) => _inbound.Add(middleware);

    /// <summary>
    /// Adds a middleware component to be executed after the main handler.
    /// </summary>
    /// <param name="middleware">The middleware to be added.</param>
    /// <returns>The current pipeline instance for chaining.</returns>
    public void UseOutbound(IPacketMiddleware<TPacket> middleware) => _outbound.Add(middleware);

    /// <summary>
    /// Executes the pipeline asynchronously using the provided packet context and terminal handler.
    /// Middlewares are invoked in the order they were added, forming a chain of responsibility.
    /// </summary>
    /// <param name="context">The packet context to be processed.</param>
    /// <param name="handler">The final delegate to be called after all middleware components.</param>
    /// <returns>A task representing the asynchronous pipeline execution.</returns>
    public System.Threading.Tasks.Task ExecuteAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> handler)
    {
        return ExecuteMiddlewareChain(this._inbound, context, async () =>
        {
            await handler().ConfigureAwait(false);
            await ExecuteMiddlewareChain(this._outbound, context, () => System.Threading.Tasks.Task.CompletedTask).ConfigureAwait(false);
        });
    }

    private static System.Threading.Tasks.Task ExecuteMiddlewareChain(
        System.Collections.Generic.List<IPacketMiddleware<TPacket>> middlewares,
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> final)
    {
        System.Func<System.Threading.Tasks.Task> next = final;

        for (System.Int32 i = middlewares.Count - 1; i >= 0; i--)
        {
            IPacketMiddleware<TPacket> current = middlewares[i];
            System.Func<System.Threading.Tasks.Task> localNext = next;

            next = () => current.InvokeAsync(context, localNext);
        }

        return next();
    }
}
