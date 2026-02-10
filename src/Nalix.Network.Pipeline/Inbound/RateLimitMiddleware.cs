// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Pipeline.Throttling;

namespace Nalix.Network.Pipeline.Inbound;

/// <summary>
/// Middleware that enforces rate limiting for inbound packets based on the remote IP address.
/// </summary>
[MiddlewareOrder(50)] // Execute after security checks
[MiddlewareStage(MiddlewareStage.Inbound)]
public class RateLimitMiddleware : IPacketMiddleware<IPacket>
{
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private readonly ILogger? _logger;
    private readonly PolicyRateLimiter _policy;
    private readonly TokenBucketLimiter _global;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware"/> class
    /// using rate limit options retrieved from the global configuration store.
    /// </summary>
    public RateLimitMiddleware()
    {
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        _policy = InstanceManager.Instance.GetOrCreateInstance<PolicyRateLimiter>();
        _global = InstanceManager.Instance.GetOrCreateInstance<TokenBucketLimiter>();
    }

    /// <inheritdoc/>
    public RateLimitMiddleware(ILogger logger, PolicyRateLimiter policyRate, TokenBucketLimiter tokenBucket)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _policy = policyRate ?? throw new ArgumentNullException(nameof(policyRate));
        _global = tokenBucket ?? throw new ArgumentNullException(nameof(tokenBucket));

    }

    /// <summary>
    /// Invokes the rate limiting middleware for inbound packets. Checks if the packet exceeds the configured rate limit for the remote IP address.
    /// If the rate limit is exceeded, the packet is not processed further.
    /// </summary>
    /// <param name="context">The packet context containing the packet, connection, and metadata.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        ArgumentNullException.ThrowIfNull(next);
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
                decision = _policy.Evaluate(context.Packet.OpCode, context);
            }
            else
            {
                // No attribute: fallback to a global per-endpoint limiter
                decision = _global.Evaluate(context.Connection.NetworkEndpoint);
            }
        }
        catch (ObjectDisposedException)
        {
            // If the limiter has been disposed (e.g., during shutdown), allow the packet to proceed
            _logger?.Debug($"[NW.{nameof(RateLimitMiddleware)}:Invoke] rate-limiter-disposed request-allowed");

            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        if (!decision.Allowed)
        {
            Directive directive = s_pool.Get<Directive>();

            // Unified response format: FAIL + RETRY (consistent with RateLimitMiddleware)
            try
            {
                directive.Initialize(
                    ControlType.FAIL, ProtocolReason.RATE_LIMITED, ProtocolAdvice.RETRY,
                    sequenceId: context.Packet.SequenceId,
                    flags: ControlFlags.IS_TRANSIENT,
                    arg0: context.Attributes.PacketOpcode?.OpCode ?? 0u,
                    arg1: (uint)decision.RetryAfterMs,
                    arg2: decision.Credit);

                using BufferLease lease = BufferLease.Rent(directive.Length + 32);

                int length = directive.Serialize(lease.SpanFull);
                lease.CommitLength(length);
                await context.Connection.TCP.SendAsync(lease.Memory).ConfigureAwait(false);
            }
            finally
            {
                s_pool.Return(directive);
            }

            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}
