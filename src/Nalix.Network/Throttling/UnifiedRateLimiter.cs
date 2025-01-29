// Copyright (c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Common.Abstractions;
using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Throttling;


/// <summary>
/// Provides a high-performance, sharded, and thread-safe rate limiter for network operations,
/// supporting per-endpoint and per-operation token bucket policies with automatic cleanup and
/// quantization of rate limit tiers. Designed for use in the Nalix network stack to enforce
/// request throttling, burst control, and abuse prevention.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="UnifiedRateLimiter"/> class implements a unified rate limiting mechanism
/// using token bucket algorithms, sharded policy storage, and efficient endpoint tracking.
/// It supports both soft throttling and hard lockout scenarios, and is optimized for
/// high-throughput, low-latency environments.
/// </para>
/// <para>
/// This class is thread-safe and intended for use as a singleton or shared service.
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class UnifiedRateLimiter : System.IDisposable
{
    #region Constants & Configuration

    private const System.Int32 PolicyTtlSeconds = 1800; // 30 minutes
    private const System.Int32 EndpointTtlSeconds = 300; // 5 minutes
    private const System.Int32 SweepEveryNChecks = 2048;
    private const System.Int32 ShardCount = 64; // Must be power of 2

    #endregion

    #region Pre-computed Data

    private static readonly System.Int32[] RpsTiers = [1, 2, 4, 8, 16, 32, 64, 128, 256, 512];
    private static readonly System.Int32[] BurstTiers = [1, 2, 4, 8, 16, 32, 64, 128, 256];

    // Cached decisions to avoid allocations
    private static readonly LimitDecision AllowedDecision = new()
    {
        Allowed = true,
        RetryAfterMs = 0,
        Credit = System.UInt16.MaxValue,
        Reason = RateLimitReason.None
    };

    private static readonly LimitDecision HardDeniedDecision = new()
    {
        Allowed = false,
        RetryAfterMs = System.Int32.MaxValue,
        Credit = 0,
        Reason = RateLimitReason.HardLockout
    };

    #endregion

    #region Core Data Structures

    private readonly record struct Policy(System.Int32 Rps, System.Int32 Burst) : System.IComparable<Policy>
    {
        public System.Int32 CompareTo(Policy other)
        {
            var result = Rps.CompareTo(other.Rps);
            return result != 0 ? result : Burst.CompareTo(other.Burst);
        }
    }

    private readonly struct CompositeKey : System.IEquatable<CompositeKey>
    {
        private readonly System.UInt16 _opCode;
        private readonly INetworkEndpoint _endpoint;
        private readonly System.Int32 _hashCode;

        public CompositeKey(System.UInt16 opCode, INetworkEndpoint endpoint)
        {
            _opCode = opCode;
            _endpoint = endpoint;
            _hashCode = System.HashCode.Combine(_opCode, endpoint?.GetHashCode() ?? 0);
        }

        public System.String Address => $"op:{_opCode}|ep:{_endpoint?.Address}";

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override System.Int32 GetHashCode() => _hashCode;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Equals(CompositeKey other) =>
            _opCode == other._opCode && ReferenceEquals(_endpoint, other._endpoint);

        public override System.Boolean Equals(System.Object obj) =>
            obj is CompositeKey other && Equals(other);
    }

    /// <summary>
    /// Per-endpoint token bucket state
    /// </summary>
    private sealed class EndpointBucket(System.Int64 now, System.Int64 initialBalance)
    {
        private readonly System.Object _gate = new();
        private System.Int64 _lastRefillTicks = now;
        private System.Int64 _microBalance = initialBalance;
        private System.Int64 _hardBlockedUntil = 0;
        private System.Int64 _lastAccessTicks = now;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public LimitDecision TryConsume(System.Int64 now, Policy policy, TokenBucketOptions options)
        {
            lock (_gate)
            {
                _lastAccessTicks = now;

                // Check hard lockout
                if (_hardBlockedUntil > now)
                {
                    var retryMs = ComputeRetryMs(now, _hardBlockedUntil);
                    return new LimitDecision
                    {
                        Allowed = false,
                        RetryAfterMs = retryMs,
                        Credit = GetCredit(_microBalance, options.TokenScale),
                        Reason = RateLimitReason.HardLockout
                    };
                }

                // Refill tokens
                RefillTokens(now, policy, options);

                // Try to consume one token
                if (_microBalance >= options.TokenScale)
                {
                    _microBalance -= options.TokenScale;
                    return new LimitDecision
                    {
                        Allowed = true,
                        RetryAfterMs = 0,
                        Credit = GetCredit(_microBalance, options.TokenScale),
                        Reason = RateLimitReason.None
                    };
                }

                // Not enough tokens - apply throttling
                var needed = options.TokenScale - _microBalance;
                var retryAfter = ComputeRetryMsForMicro(needed, policy.Rps);

                // Set hard lockout if configured
                if (options.HardLockoutSeconds > 0)
                {
                    _hardBlockedUntil = now + ToTicks(options.HardLockoutSeconds);
                    return new LimitDecision
                    {
                        Allowed = false,
                        RetryAfterMs = retryAfter,
                        Credit = GetCredit(_microBalance, options.TokenScale),
                        Reason = RateLimitReason.HardLockout
                    };
                }

                return new LimitDecision
                {
                    Allowed = false,
                    RetryAfterMs = retryAfter,
                    Credit = GetCredit(_microBalance, options.TokenScale),
                    Reason = RateLimitReason.SoftThrottle
                };
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void RefillTokens(System.Int64 now, Policy policy, TokenBucketOptions options)
        {
            var elapsed = now - _lastRefillTicks;
            if (elapsed <= 0)
            {
                return;
            }

            var maxCapacity = (System.Int64)policy.Burst * options.TokenScale;
            if (_microBalance >= maxCapacity)
            {
                return;
            }

            var refillRate = (System.Int64)policy.Rps * options.TokenScale;
            var tokensToAdd = elapsed * refillRate / System.TimeSpan.TicksPerSecond;

            if (tokensToAdd > 0)
            {
                _microBalance = System.Math.Min(_microBalance + tokensToAdd, maxCapacity);
                _lastRefillTicks = now;
            }
        }

        public System.Boolean IsStale(System.Int64 now, System.Int32 staleSeconds) =>
            now - _lastAccessTicks > ToTicks(staleSeconds);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static System.UInt16 GetCredit(System.Int64 microBalance, System.Int32 tokenScale)
        {
            var tokens = microBalance / tokenScale;
            return tokens <= 0 ? (System.UInt16)0 :
                   tokens >= System.UInt16.MaxValue ? System.UInt16.MaxValue :
                   (System.UInt16)tokens;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static System.Int32 ComputeRetryMs(System.Int64 now, System.Int64 until)
        {
            if (until <= now)
            {
                return 0;
            }

            var ms = (until - now) / System.TimeSpan.TicksPerMillisecond;
            return ms > System.Int32.MaxValue ? System.Int32.MaxValue : (System.Int32)ms;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static System.Int32 ComputeRetryMsForMicro(System.Int64 microNeeded, System.Int32 rps)
            => rps <= 0 ? 0 : (System.Int32)System.Math.Ceiling(microNeeded * 1000.0 / (rps * 1000000.0));

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static System.Int64 ToTicks(System.Int32 seconds) => seconds * System.TimeSpan.TicksPerSecond;
    }

    /// <summary>
    /// Policy-level container holding all endpoints for that policy
    /// </summary>
    private sealed class PolicyContainer
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<CompositeKey, EndpointBucket> _endpoints = new();
        private System.Int64 _lastAccessTicks;

        public PolicyContainer() => Touch();

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Touch() => _lastAccessTicks = System.DateTime.UtcNow.Ticks;

        public LimitDecision CheckEndpoint(CompositeKey key, Policy policy, TokenBucketOptions options)
        {
            Touch();

            var now = System.DateTime.UtcNow.Ticks;
            var bucket = _endpoints.GetOrAdd(key, _ => new EndpointBucket(now, 0));

            return bucket.TryConsume(now, policy, options);
        }

        public System.Boolean IsStale(System.Int64 now, System.Int32 staleSeconds) =>
            now - _lastAccessTicks > staleSeconds * System.TimeSpan.TicksPerSecond;

        public System.Int32 CleanupStaleEndpoints(System.Int32 staleSeconds)
        {
            var now = System.DateTime.UtcNow.Ticks;
            var removed = 0;

            foreach (var (key, bucket) in _endpoints)
            {
                if (bucket.IsStale(now, staleSeconds))
                {
                    if (_endpoints.TryRemove(key, out _))
                    {
                        removed++;
                    }
                }
            }

            return removed;
        }

        public System.Int32 EndpointCount => _endpoints.Count;
    }

    #endregion

    #region Fields

    private readonly PolicyShard[] _shards;
    private readonly TokenBucketOptions _options;
    private readonly ILogger _logger;

    [System.ThreadStatic]
    private static System.Int32 t_checkCounter;

    private System.Boolean _disposed;

    #endregion

    #region Sharded Policy Storage

    private sealed class PolicyShard
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Policy, PolicyContainer> _policies = new();

        public LimitDecision Check(CompositeKey key, Policy policy, TokenBucketOptions options)
        {
            var container = _policies.GetOrAdd(policy, _ => new PolicyContainer());
            return container.CheckEndpoint(key, policy, options);
        }

        public void Cleanup(System.Int32 policyTtlSeconds, System.Int32 endpointTtlSeconds)
        {
            var now = System.DateTime.UtcNow.Ticks;
            var policiesToRemove = new System.Collections.Generic.List<Policy>();

            foreach (var (policy, container) in _policies)
            {
                // Cleanup stale endpoints within each policy
                container.CleanupStaleEndpoints(endpointTtlSeconds);

                // Remove empty or stale policy containers
                if (container.EndpointCount == 0 || container.IsStale(now, policyTtlSeconds))
                {
                    policiesToRemove.Add(policy);
                }
            }

            foreach (var policy in policiesToRemove)
            {
                _policies.TryRemove(policy, out _);
            }
        }

        public System.Int32 PolicyCount => _policies.Count;

        public Policy FindNearestPolicy(Policy wanted)
        {
            var best = default(Policy);
            var bestDistance = System.Int32.MaxValue;

            foreach (var policy in _policies.Keys)
            {
                var distance = System.Math.Abs(policy.Rps - wanted.Rps) + System.Math.Abs(policy.Burst - wanted.Burst);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = policy;
                    if (distance == 0)
                    {
                        break;
                    }
                }
            }

            return best;
        }
    }

    #endregion

    #region Constructor & Disposal

    /// <inheritdoc/>
    public UnifiedRateLimiter(TokenBucketOptions options = null)
    {
        _options = options ?? ConfigurationManager.Instance.Get<TokenBucketOptions>();
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        _shards = new PolicyShard[ShardCount];
        for (System.Int32 i = 0; i < ShardCount; i++)
        {
            _shards[i] = new PolicyShard();
        }

        _logger?.Info($"[UnifiedRateLimiter] Initialized with {ShardCount} shards");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger?.Info("[UnifiedRateLimiter] Disposed");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Main entry point for rate limiting checks
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public LimitDecision Check(System.UInt16 opCode, PacketContext<IPacket> context)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(UnifiedRateLimiter));

        PacketRateLimitAttribute rateLimitAttr = context.Attributes.RateLimit!;

        // Fast path for unlimited
        if (rateLimitAttr.RequestsPerSecond <= 0)
        {
            return AllowedDecision;
        }

        if (rateLimitAttr.Burst <= 0)
        {
            return HardDeniedDecision;
        }

        // Quantize to reduce policy variations
        System.Int32 rps = QuantizeFast(rateLimitAttr.RequestsPerSecond, RpsTiers);
        System.Int32 burst = QuantizeFast(rateLimitAttr.Burst, BurstTiers);
        Policy policy = new(rps, burst);

        // Create composite key
        CompositeKey key = new(opCode, context.Connection.EndPoint);

        // Route to appropriate shard
        PolicyShard shard = GetShard(policy);
        LimitDecision decision = shard.Check(key, policy, _options);

        // Periodic cleanup (thread-local counter reduces contention)
        if ((++t_checkCounter & (SweepEveryNChecks - 1)) == 0)
        {
            _ = System.Threading.Tasks.Task.Run(PerformCleanup);
        }

        return decision;
    }

    #endregion

    #region Helper Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private PolicyShard GetShard(Policy policy)
    {
        System.Int32 hash = policy.GetHashCode();
        return _shards[(System.UInt32)hash % ShardCount];
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 QuantizeFast(System.Int32 value, System.Int32[] tiers)
    {
        // Binary search for efficiency
        System.Int32 left = 0;
        System.Int32 right = tiers.Length - 1;

        while (left <= right)
        {
            System.Int32 mid = (left + right) >> 1;
            if (tiers[mid] >= value)
            {
                if (mid == 0 || tiers[mid - 1] < value)
                {
                    return tiers[mid];
                }

                right = mid - 1;
            }
            else
            {
                left = mid + 1;
            }
        }

        return tiers[^1];
    }

    private void PerformCleanup()
    {
        try
        {
            System.Threading.Tasks.Parallel.ForEach(_shards, shard => shard.Cleanup(PolicyTtlSeconds, EndpointTtlSeconds));
        }
        catch (System.Exception ex)
        {
            _logger?.Error($"[UnifiedRateLimiter] Cleanup error: {ex.Message}");
        }
    }

    #endregion

    #region Decision & Reason Types

    /// <summary>
    /// Represents the result of a rate limiting decision, containing information about whether
    /// the request was allowed and providing guidance for retry behavior.
    /// </summary>
    /// <remarks>
    /// This structure is returned by rate limiting operations to inform callers about:
    /// <list type="bullet">
    /// <item><description>Whether the current request should be processed</description></item>
    /// <item><description>How long to wait before retrying if denied</description></item>
    /// <item><description>Available token credit for flow control</description></item>
    /// <item><description>The specific reason for any throttling action</description></item>
    /// </list>
    /// </remarks>
    public readonly struct LimitDecision
    {
        /// <summary>
        /// Gets a value indicating whether the request is allowed to proceed.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the request should be processed;
        /// <see langword="false"/> if the request should be throttled or rejected.
        /// </value>
        /// <remarks>
        /// When <see langword="true"/>, the caller should process the request normally.
        /// When <see langword="false"/>, the caller should reject the request and
        /// optionally use <see cref="RetryAfterMs"/> to inform the client when to retry.
        /// </remarks>
        public System.Boolean Allowed { get; init; }

        /// <summary>
        /// Gets the number of milliseconds the client should wait before retrying the request.
        /// </summary>
        /// <value>
        /// The retry delay in milliseconds.  Returns 0 if no specific retry time is recommended,
        /// or if the request was allowed.
        /// </value>
        /// <remarks>
        /// <para>
        /// This value provides precise backoff timing to help clients implement proper retry logic.
        /// A value of 0 indicates either the request was allowed or no specific retry timing is available.
        /// </para>
        /// <para>
        /// For hard lockouts, this may represent the full lockout duration.
        /// For soft throttling, this represents the estimated time until tokens become available.
        /// </para>
        /// </remarks>
        public System.Int32 RetryAfterMs { get; init; }

        /// <summary>
        /// Gets the number of tokens (request capacity) remaining after this decision.
        /// </summary>
        /// <value>
        /// The remaining token count as a 16-bit unsigned integer.
        /// Higher values indicate more available capacity for subsequent requests.
        /// </value>
        /// <remarks>
        /// <para>
        /// This credit information can be used by clients for flow control and adaptive behavior.
        /// A value of 0 indicates no tokens are available, while <see cref="System.UInt16.MaxValue"/>
        /// typically indicates unlimited or very high capacity.
        /// </para>
        /// <para>
        /// The credit reflects the state after the current decision, so it accounts for any
        /// token consumption that occurred during this check.
        /// </para>
        /// </remarks>
        public System.UInt16 Credit { get; init; }

        /// <summary>
        /// Gets the specific reason for the rate limiting decision.
        /// </summary>
        /// <value>
        /// A <see cref="RateLimitReason"/> value indicating why the request was throttled,
        /// or <see cref="RateLimitReason.None"/> if the request was allowed.
        /// </value>
        /// <remarks>
        /// This property provides detailed context about the throttling decision, allowing
        /// callers to implement appropriate responses (e.g., different retry strategies
        /// for soft throttles vs.  hard lockouts).
        /// </remarks>
        public RateLimitReason Reason { get; init; }
    }

    /// <summary>
    /// Specifies the reason for a rate limiting decision, indicating the type and severity
    /// of throttling applied to a request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration helps distinguish between different types of rate limiting scenarios,
    /// allowing clients and middleware to implement appropriate response strategies.
    /// </para>
    /// <para>
    /// The enumeration is designed as a byte-sized type for memory efficiency in high-throughput scenarios.
    /// </para>
    /// </remarks>
    public enum RateLimitReason : System.Byte
    {
        /// <summary>
        /// No rate limiting was applied; the request was allowed to proceed normally.
        /// </summary>
        /// <remarks>
        /// This is the default value indicating that the request passed all rate limiting checks
        /// and should be processed without any throttling concerns.
        /// </remarks>
        None = 0,

        /// <summary>
        /// The request was denied due to soft throttling, typically when the rate limit
        /// is exceeded but recovery is expected soon.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Soft throttling indicates temporary capacity exhaustion where tokens will be
        /// replenished according to the configured refill rate.  Clients should implement
        /// exponential backoff and retry after the suggested delay.
        /// </para>
        /// <para>
        /// This is generally a transient condition that resolves as the token bucket refills.
        /// </para>
        /// </remarks>
        SoftThrottle = 1,

        /// <summary>
        /// The request was denied due to hard lockout, typically after repeated violations
        /// or exceeding critical thresholds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Hard lockout represents a more severe throttling condition, often triggered by
        /// aggressive or abusive request patterns. The client is temporarily blocked for
        /// a configured duration regardless of normal token replenishment.
        /// </para>
        /// <para>
        /// Clients should implement longer backoff periods and may need to reduce their
        /// overall request rate to avoid future lockouts.
        /// </para>
        /// </remarks>
        HardLockout = 2
    }

    #endregion Decision & Reason Types
}