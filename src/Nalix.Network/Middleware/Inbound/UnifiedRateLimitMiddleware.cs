// Copyright (c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connections;
using Nalix.Network.Dispatch;
using Nalix.Network.Throttling;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Unified rate limiting middleware that handles both global and policy-based rate limiting.
/// Uses tiered approach:  Global limiter first, then policy-specific limiter if configured.
/// </summary>
public class UnifiedRateLimitMiddleware : IPacketMiddleware<IPacket>
{
    private readonly UnifiedRateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the unified rate limiting middleware.
    /// </summary>
    public UnifiedRateLimitMiddleware() => _rateLimiter = InstanceManager.Instance.GetOrCreateInstance<UnifiedRateLimiter>();

    /// <summary>
    /// Applies comprehensive rate limiting logic to incoming packets.
    /// Supports both global endpoint-based limiting and packet-specific policy limiting.
    /// </summary>
    /// <param name="context">The packet context containing packet, connection, and metadata.</param>
    /// <param name="next">Delegate representing the next middleware to execute.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        // Validate connection endpoint
        if (System.String.IsNullOrEmpty(context.Connection.EndPoint?.Address))
        {
            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.MALFORMED_PACKET,
                action: ProtocolAdvice.RETRY,
                sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                flags: ControlFlags.IS_TRANSIENT).ConfigureAwait(false);

            return;
        }

        // Get rate limit decision
        UnifiedRateLimiter.LimitDecision decision = GetRateLimitDecision(context);

        if (!decision.Allowed)
        {
            ControlFlags flags = ControlFlags.IS_TRANSIENT;
            System.UInt32 sequenceId = (context.Packet as IPacketSequenced)?.SequenceId ?? 0;
            ControlType controlType = decision.Reason == UnifiedRateLimiter.RateLimitReason.HardLockout
                ? ControlType.FAIL
                : ControlType.THROTTLE;

            ProtocolAdvice advice = decision.Reason == UnifiedRateLimiter.RateLimitReason.HardLockout
                ? ProtocolAdvice.BACKOFF_RETRY
                : ProtocolAdvice.RETRY;

            if (decision.Reason == UnifiedRateLimiter.RateLimitReason.SoftThrottle)
            {
                flags |= ControlFlags.SLOW_DOWN;
            }

            await context.Connection.SendAsync(
                controlType: controlType,
                reason: ProtocolReason.RATE_LIMITED,
                action: advice,
                sequenceId: sequenceId,
                flags: flags,
                arg0: context.Attributes.OpCode?.OpCode ?? context.Packet.OpCode,
                arg1: (System.UInt32)System.Math.Max(0, decision.RetryAfterMs), arg2: decision.Credit).ConfigureAwait(false);

            return;
        }

        // Continue to next middleware
        await next(context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines rate limit decision based on available attributes and configuration.
    /// </summary>
    private UnifiedRateLimiter.LimitDecision GetRateLimitDecision(PacketContext<IPacket> context)
    {
        var rateLimitAttr = context.Attributes.RateLimit;

        // If no rate limit attribute, use permissive defaults
        if (rateLimitAttr is null)
        {
            // You can either:
            // 1. Allow unlimited (current approach)
            return new UnifiedRateLimiter.LimitDecision
            {
                Allowed = true,
                RetryAfterMs = 0,
                Credit = System.UInt16.MaxValue,
                Reason = UnifiedRateLimiter.RateLimitReason.None
            };

            // 2. OR apply a global default policy (recommended)
            // rateLimitAttr = GetGlobalDefaultPolicy();
        }

        // Use unified rate limiter for policy-based limiting
        return _rateLimiter.Check(context.Packet.OpCode, context);
    }
}