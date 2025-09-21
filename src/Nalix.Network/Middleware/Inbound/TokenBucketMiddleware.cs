// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Attributes;
using Nalix.Common.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch;
using Nalix.Network.Throttling;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that enforces rate limiting for incoming packets.
/// If a connection exceeds the allowed request rate, a rate limit response is sent
/// and further processing is halted.
/// </summary>
[Middleware(MiddlewareStage.Inbound, order: 0, name: "RateLimit")]
public class TokenBucketMiddleware : IPacketMiddleware<IPacket>
{
    private readonly TokenBucketLimiter _limiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenBucketMiddleware"/> class
    /// using rate limit options retrieved from the global configuration store.
    /// </summary>
    public TokenBucketMiddleware()
    {
        _limiter = InstanceManager.Instance.GetOrCreateInstance<TokenBucketLimiter>();
    }

    /// <summary>
    /// Applies rate limiting logic to incoming packets based on their connection's endpoint.
    /// If the limit is exceeded, a warning packet is sent and the pipeline terminates early.
    /// Otherwise, the next middleware in the sequence is invoked.
    /// </summary>
    /// <param name="context">The packet context containing both the packet and the connection.</param>
    /// <param name="next">A delegate representing the next middleware to be executed.</param>
    /// <param name="ct"></param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next,
        System.Threading.CancellationToken ct)
    {
        TokenBucketLimiter.LimitDecision decision = _limiter.Check(context.Connection.RemoteEndPoint);

        if (!decision.Allowed)
        {
            await context.Connection.SendAsync(
                ControlType.THROTTLE,
                ProtocolCode.RATE_LIMITED,
                ProtocolAction.BACKOFF_RETRY,
                sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                flags: ControlFlags.SLOW_DOWN | ControlFlags.IS_TRANSIENT,
                arg0: (System.UInt32)System.Math.Max(0, (decision.RetryAfterMs + 99) / 100), // steps of 100ms
                arg1: 0, arg2: decision.Credit).ConfigureAwait(false);

            return;
        }

        await next(ct).ConfigureAwait(false);
    }
}