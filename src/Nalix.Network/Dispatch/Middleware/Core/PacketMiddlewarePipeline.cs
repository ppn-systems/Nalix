using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Interfaces;

namespace Nalix.Network.Dispatch.Middleware.Core;

/// <summary>
/// Represents a middleware pipeline for processing packets.
/// Allows chaining multiple middleware components to handle a packet context.
/// </summary>
/// <typeparam name="TPacket">The type of packet being processed in the pipeline.</typeparam>
public class PacketMiddlewarePipeline<TPacket>
{
    private readonly System.Collections.Generic.List<IPacketMiddleware<TPacket>> _pre = [];
    private readonly System.Collections.Generic.List<IPacketMiddleware<TPacket>> _post = [];

    /// <summary>
    /// Adds a middleware component to the pipeline.
    /// </summary>
    /// <param name="middleware">The middleware to be added.</param>
    /// <returns>The current pipeline instance for chaining.</returns>
    public PacketMiddlewarePipeline<TPacket> UsePre(IPacketMiddleware<TPacket> middleware)
    {
        this._pre.Add(middleware);
        return this;
    }

    /// <summary>
    /// Adds a middleware component to the pipeline.
    /// </summary>
    /// <param name="middleware">The middleware to be added.</param>
    /// <returns>The current pipeline instance for chaining.</returns>
    public PacketMiddlewarePipeline<TPacket> UsePost(IPacketMiddleware<TPacket> middleware)
    {
        this._post.Add(middleware);
        return this;
    }

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
        return ExecuteMiddlewareChain(this._pre, context, async () =>
        {
            await handler();
            await ExecuteMiddlewareChain(this._post, context, () => System.Threading.Tasks.Task.CompletedTask);
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