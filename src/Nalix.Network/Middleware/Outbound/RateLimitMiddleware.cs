// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Packets.Enums;
using Nalix.Network.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Connection;
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
        TokenBucketOptions option = ConfigurationManager.Instance.Get<TokenBucketOptions>();
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
        System.String key = context.Connection.RemoteEndPoint.ToString() ?? "unknown";
        RequestLimiter.LimitDecision decision = this._limiter.Check(key);

        if (!decision.Allowed)
        {
            System.UInt32 sequenceId = 0;
            if (context.Packet is IPacketSequenced s)
            {
                sequenceId = s.SequenceId;
            }

            await context.Connection.SendAsync(
                ControlType.THROTTLE,
                ReasonCode.RATE_LIMITED,
                SuggestedAction.BACKOFF_RETRY,
                sequenceId: sequenceId,
                flags: ControlFlags.SLOW_DOWN | ControlFlags.IS_TRANSIENT,
                arg0: (System.UInt32)System.Math.Max(0, (decision.RetryAfterMs + 99) / 100), // steps of 100ms
                arg1: 0, arg2: decision.Credit).ConfigureAwait(false);

            return;
        }

        await next();
    }
}