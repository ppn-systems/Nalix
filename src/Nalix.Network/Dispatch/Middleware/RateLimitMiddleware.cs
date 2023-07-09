using Nalix.Common.Package;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;
using Nalix.Network.Security.Guard;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Dispatch.Middleware;

/// <summary>
/// Middleware that enforces rate limiting for incoming packets.
/// If a connection exceeds the allowed request rate, a rate limit response is sent
/// and further processing is halted.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type, which must implement both <see cref="IPacket"/> and <see cref="IPacketFactory{TPacket}"/>.
/// </typeparam>
public class RateLimitMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketFactory<TPacket>
{
    private readonly RequestLimiter _limiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware{TPacket}"/> class
    /// using rate limit options retrieved from the global configuration store.
    /// </summary>
    public RateLimitMiddleware()
    {
        RequestRateLimitOptions option = ConfigurationStore.Instance.Get<RequestRateLimitOptions>();
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
            _ = await context.Connection.Tcp.SendAsync(TPacket.Create(0, "You have been rate limited."));
            return;
        }

        await next();
    }
}