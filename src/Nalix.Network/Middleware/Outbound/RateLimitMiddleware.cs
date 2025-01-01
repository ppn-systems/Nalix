// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Packets.Enums;
using Nalix.Network.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch;
using Nalix.Network.Throttling;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Middleware.Outbound;

/// <summary>
/// Middleware that enforces rate limiting for incoming packets.
/// If a connection exceeds the allowed request rate, a rate limit response is sent
/// and further processing is halted.
/// </summary>
[PacketMiddleware(MiddlewareStage.Outbound, order: 0, name: "RateLimit")]
public class RateLimitMiddleware : IPacketMiddleware<IPacket>
{
    private readonly RequestLimiter _limiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware"/> class
    /// using rate limit options retrieved from the global configuration store.
    /// </summary>
    public RateLimitMiddleware()
    {
        RateLimitOptions option = ConfigurationManager.Instance.Get<RateLimitOptions>();
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
        PacketContext<IPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        if (!this._limiter.CheckLimit(context.Connection.RemoteEndPoint.ToString() ?? "unknown"))
        {
            _ = await context.Connection.Tcp.SendAsync("You have been rate limited.").ConfigureAwait(false);
        }

        await next();
    }
}