// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Routing;
using Nalix.Network.Throttling;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that enforces rate limiting for inbound packets based on the remote IP address.
/// </summary>
[MiddlewareOrder(50)] // Execute after security checks
[MiddlewareStage(MiddlewareStage.Inbound)]
public class RateLimitMiddleware : IPacketMiddleware<IPacket>
{
    private readonly ILogger? s_logger;
    private readonly TokenBucketLimiter s_Global;
    private readonly PolicyRateLimiter s_PolicyRateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware"/> class
    /// using rate limit options retrieved from the global configuration store.
    /// </summary>
    public RateLimitMiddleware()
    {
        s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        s_Global = InstanceManager.Instance.GetOrCreateInstance<TokenBucketLimiter>();
        s_PolicyRateLimiter = InstanceManager.Instance.GetOrCreateInstance<PolicyRateLimiter>();
    }

    /// <summary>
    /// Invokes the rate limiting middleware for inbound packets. Checks if the packet exceeds the configured rate limit for the remote IP address.
    /// If the rate limit is exceeded, the packet is not processed further.
    /// </summary>
    /// <param name="context">The packet context containing the packet, connection, and metadata.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(
        PacketContext<IPacket> context,
        Func<CancellationToken, Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Attributes.RateLimit is null)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        TokenBucketLimiter.RateLimitDecision decision;
        PacketRateLimitAttribute? rl = context.Attributes.RateLimit;

        try
        {

            if (rl is not null)
            {
                // Attribute-driven policy: use centralized policy-based limiter
                decision = s_PolicyRateLimiter.Check(context.Packet.OpCode, context);
            }
            else
            {
                // No attribute: fallback to a global per-endpoint limiter
                decision = s_Global.Check(context.Connection.NetworkEndpoint);
            }
        }
        catch (ObjectDisposedException)
        {
            // If the limiter has been disposed (e.g., during shutdown), allow the packet to proceed
            s_logger?.Debug($"[NW.{nameof(RateLimitMiddleware)}:Invoke] rate-limiter-disposed request-allowed");

            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        if (!decision.Allowed)
        {
            uint sequenceId = context.Packet is IPacketSequenced sequenced
                ? sequenced.SequenceId
                : 0;

            // Unified response format: FAIL + RETRY (consistent with RateLimitMiddleware)
            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.RATE_LIMITED,
                action: ProtocolAdvice.RETRY,
                sequenceId: sequenceId,
                flags: ControlFlags.IsTransient,
                arg0: context.Attributes.PacketOpcode?.OpCode ?? 0u,
                arg1: (uint)decision.RetryAfterMs,
                arg2: decision.Credit
            ).ConfigureAwait(false);

            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}
