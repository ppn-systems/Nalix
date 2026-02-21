// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Enums;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Packets.Attributes;
using Nalix.Common.Messaging.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connections;
using Nalix.Network.Dispatch;
using Nalix.Network.Throttling;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that enforces rate limiting for inbound packets based on the remote IP address.
/// </summary>
[MiddlewareOrder(50)] // Execute after security checks
[MiddlewareStage(MiddlewareStage.Inbound)]
public class RateLimitMiddleware : IPacketMiddleware<IPacket>
{
    private readonly TokenBucketLimiter _global;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware"/> class
    /// using rate limit options retrieved from the global configuration store.
    /// </summary>
    public RateLimitMiddleware() => _global = InstanceManager.Instance.GetOrCreateInstance<TokenBucketLimiter>();

    /// <summary>
    /// Invokes the rate limiting middleware for inbound packets. Checks if the packet exceeds the configured rate limit for the remote IP address.
    /// If the rate limit is exceeded, the packet is not processed further.
    /// </summary>
    /// <param name="context">The packet context containing the packet, connection, and metadata.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        // Validate input
        // Determine which limiter to call
        TokenBucketLimiter.RateLimitDecision decision;
        System.ArgumentNullException.ThrowIfNull(context);
        PacketRateLimitAttribute rl = context.Attributes.RateLimit;

        if (rl is not null)
        {
            // Attribute-driven policy: use centralized policy-based limiter
            decision = PolicyRateLimiter.Check(context.Packet.OpCode, context);
        }
        else
        {
            // No attribute: fallback to a global per-endpoint limiter
            decision = _global.Check(context.Connection.EndPoint);
        }

        if (!decision.Allowed)
        {
            System.UInt32 sequenceId = context.Packet is IPacketSequenced sequenced
                ? sequenced.SequenceId
                : 0;

            // Unified response format: FAIL + RETRY (consistent with RateLimitMiddleware)
            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.RATE_LIMITED,
                action: ProtocolAdvice.RETRY,
                sequenceId: sequenceId,
                flags: ControlFlags.IS_TRANSIENT,
                arg0: context.Attributes.OpCode?.OpCode ?? 0u,
                arg1: (System.UInt32)decision.RetryAfterMs,
                arg2: decision.Credit
            ).ConfigureAwait(false);

            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}

