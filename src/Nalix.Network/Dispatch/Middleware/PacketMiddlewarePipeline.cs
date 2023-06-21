namespace Nalix.Network.Dispatch.Middleware;

/// <summary>
/// Represents a middleware pipeline for processing packets.
/// Allows chaining multiple middleware components to handle a packet context.
/// </summary>
/// <typeparam name="TPacket">The type of packet being processed in the pipeline.</typeparam>
public class PacketMiddlewarePipeline<TPacket>
{
    private readonly System.Collections.Generic.List<IPacketMiddleware<TPacket>> _middlewares = [];

    /// <summary>
    /// Adds a middleware component to the pipeline.
    /// </summary>
    /// <param name="middleware">The middleware to be added.</param>
    /// <returns>The current pipeline instance for chaining.</returns>
    public PacketMiddlewarePipeline<TPacket> Use(IPacketMiddleware<TPacket> middleware)
    {
        _middlewares.Add(middleware);
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
        System.Func<System.Threading.Tasks.Task> next = handler;
        for (System.Int32 i = _middlewares.Count - 1; i >= 0; i--)
        {
            IPacketMiddleware<TPacket> current = _middlewares[i];
            System.Func<System.Threading.Tasks.Task> localNext = next;
            next = () => current.InvokeAsync(context, localNext);
        }

        return next();
    }
}