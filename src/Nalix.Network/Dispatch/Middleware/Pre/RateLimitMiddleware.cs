using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;
using Nalix.Network.Protocols.Messages;
using Nalix.Network.Throttling;
using Nalix.Shared.Configuration;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Dispatch.Middleware.Pre;

/// <summary>
/// Middleware that enforces rate limiting for incoming packets.
/// If a connection exceeds the allowed request rate, a rate limit response is sent
/// and further processing is halted.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type, which must implement both <see cref="IPacket"/> and <see cref="IPacketTransformer{TPacket}"/>.
/// </typeparam>
[PacketMiddleware(MiddlewareStage.Pre, order: 0, name: "RateLimit")]
public class RateLimitMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
    private readonly RequestLimiter _limiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware{TPacket}"/> class
    /// using rate limit options retrieved from the global configuration store.
    /// </summary>
    public RateLimitMiddleware()
    {
        RateLimitOptions option = ConfigurationStore.Instance.Get<RateLimitOptions>();
        this._limiter = new RequestLimiter(option);
    }

    /// <summary>
    /// Applies rate limiting logic to incoming packets based on their connection's endpoint.
    /// If the limit is exceeded, a warning packet is sent and the pipeline terminates early.
    /// Otherwise, the next middleware in the sequence is invoked.
    /// </summary>
    /// <param name="context">The packet context containing both the packet and the connection.</param>
    /// <param name="next">A delegate representing the next middleware to be executed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        if (!this._limiter.CheckLimit(context.Connection.RemoteEndPoint.ToString() ?? "unknown"))
        {
            LiteralPacket text = ObjectPoolManager.Instance.Get<LiteralPacket>();
            try
            {
                text.Initialize("You have been rate limited.");
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());

                return;
            }
            finally
            {
                ObjectPoolManager.Instance.Return<LiteralPacket>(text);
            }
        }

        await next();
    }
}