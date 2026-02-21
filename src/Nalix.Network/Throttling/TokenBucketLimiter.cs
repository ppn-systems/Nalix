// Copyright (c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Configurations;
using Nalix.Shared.Memory.Pools;

namespace Nalix.Network.Throttling;

/// <summary>
/// High-performance token-bucket based rate limiter with per-endpoint state,
/// using Stopwatch ticks for time arithmetic and fixed-point token precision.
/// Provides precise Retry-After and Credit for client backoff and flow control.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class TokenBucketLimiter : System.IDisposable, System.IAsyncDisposable, IReportable
{
    #region Public Types

    /// <summary>
    /// Decision result for a rate-limit check.
    /// </summary>
    public readonly struct RateLimitDecision
    {
        /// <summary>True if request is allowed (token consumed).</summary>
        public System.Boolean Allowed { get; init; }

        /// <summary>Milliseconds until at least 1 token becomes available (0 if allowed or no soft backoff).</summary>
        public System.Int32 RetryAfterMs { get; init; }

        /// <summary>Remaining whole tokens (credit) after the check.</summary>
        public System.UInt16 Credit { get; init; }

        /// <summary>Reason for throttling; NONE if allowed.</summary>
        public RateLimitReason Reason { get; init; }
    }

    /// <summary>
    /// Throttling reason taxonomy.
    /// </summary>
    public enum RateLimitReason : System.Byte
    {
        /// <summary>NONE. </summary>
        None = 0,

        /// <summary>
        /// The request was denied due to a soft throttle, typically when the rate limit is exceeded but not enough to trigger a hard lockout.
        /// </summary>
        SoftThrottle = 1,

        /// <summary>
        /// The request was denied due to a hard lockout, typically after repeated violations or exceeding a critical threshold.
        /// </summary>
        HardLockout = 2
    }

    #endregion Public Types

    #region Private Types

    /// <summary>Per-endpoint mutable state; guarded by its <see cref="Gate"/>.</summary>
    private sealed class EndpointState
    {
        public readonly System.Object Gate = new();

        public System.Int64 LastSeenSw;
        public System.Int64 MicroBalance;
        public System.Int32 SoftViolations;
        public System.Int64 LastViolationSw;
        public System.Int64 AccumulatedMicro;
        public System.Int64 LastRefillSwTicks;
        public System.Int64 HardBlockedUntilSw;
    }

    /// <summary>A shard contains a dictionary of endpoint states.</summary>
    private sealed class Shard
    {
        public readonly System.Collections.Concurrent.ConcurrentDictionary<INetworkEndpoint, EndpointState> Map = new();
    }

    /// <summary>Context for endpoint state retrieval or creation.</summary>
    private readonly struct EndpointStateResult
    {
        public EndpointState State { get; init; }
        public System.Boolean IsNew { get; init; }
        public RateLimitDecision? EarlyDecision { get; init; }
    }

    #endregion Private Types

    #region Constants

    private const System.Int32 MinReportCapacity = 256;
    private const System.Int32 MaxEvictionCapacity = 4096;
    private const System.Int32 CancellationCheckFrequency = 256;
    private const System.Double MaxDelayMs = System.Int32.MaxValue - 1000.0;

    #endregion Constants

    #region Fields

    private readonly Shard[] _shards;
    private readonly System.Double _swFreq;
    private readonly TokenBucketOptions _opt;
    private readonly System.Int64 _capacityMicro;
    private readonly System.Int64 _refillPerSecMicro;
    private readonly System.Int32 _cleanupIntervalSec;
    private readonly System.Int64 _initialBalanceMicro;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private readonly ILogger _logger;
    private System.Int32 _totalEndpointCount;
    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Creates a new TokenBucketLimiter with provided options.
    /// </summary>
    /// <param name="options">Configuration options for the limiter.</param>
    /// <exception cref="InternalErrorException">Thrown when options validation fails.</exception>
    public TokenBucketLimiter([System.Diagnostics.CodeAnalysis.AllowNull] TokenBucketOptions options = null)
    {
        _opt = options ?? ConfigurationManager.Instance.Get<TokenBucketOptions>();
        _opt.Validate();

        _totalEndpointCount = 0;
        _shards = new Shard[_opt.ShardCount];
        _swFreq = System.Diagnostics.Stopwatch.Frequency;
        _cleanupIntervalSec = _opt.CleanupIntervalSeconds;
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        _capacityMicro = (System.Int64)_opt.CapacityTokens * _opt.TokenScale;
        _refillPerSecMicro = (System.Int64)System.Math.Round(_opt.RefillTokensPerSecond * _opt.TokenScale);

        _initialBalanceMicro = CalculateInitialBalance();

        for (System.Int32 i = 0; i < _shards.Length; i++)
        {
            _shards[i] = new Shard();
        }

        ScheduleCleanupJob();
        LogInitialization();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenBucketLimiter"/> class with default options.
    /// </summary>
    public TokenBucketLimiter() : this(null) { }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Checks and consumes 1 token for the given endpoint.  Returns decision with RetryAfter and Credit.
    /// </summary>
    /// <param name="key">The network endpoint to check.</param>
    /// <returns>A decision indicating whether the request is allowed.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the limiter has been disposed.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when key address is null or empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public RateLimitDecision Check([System.Diagnostics.CodeAnalysis.NotNull] INetworkEndpoint key)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(TokenBucketLimiter));
        ValidateEndpoint(key);

        System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();
        Shard shard = SelectShard(key);

        EndpointStateResult result = GetOrCreateEndpointState(key, shard, now);

        // Early exit if limit reached during creation
        return result.EarlyDecision ?? EvaluateRateLimit(key, result.State, now);
    }

    /// <summary>
    /// Generates a human-readable diagnostic report of the limiter state.
    /// </summary>
    /// <returns>Formatted string report.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public System.String GenerateReport()
    {
        System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();

        var snapshot = CollectStateSnapshot(now, out System.Int32 totalEndpoints, out System.Int32 hardBlockedCount);

        try
        {
            return BuildReportString(snapshot, totalEndpoints, hardBlockedCount, now);
        }
        finally
        {
            ReturnSnapshotToPool(snapshot);
        }
    }

    #endregion Public API

    #region Endpoint State Management

    /// <summary>
    /// Validates the provided endpoint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ValidateEndpoint(INetworkEndpoint key)
    {
        if (key is null)
        {
            throw new System.ArgumentNullException(nameof(key), "Endpoint cannot be null");
        }

        if (System.String.IsNullOrEmpty(key.Address))
        {
            throw new System.ArgumentException("Endpoint address cannot be null or empty", nameof(key));
        }
    }

    /// <summary>
    /// Gets existing or creates new endpoint state with proper limit enforcement.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private EndpointStateResult GetOrCreateEndpointState(INetworkEndpoint key, Shard shard, System.Int64 now)
    {
        // Fast-path: endpoint already tracked
        if (shard.Map.TryGetValue(key, out EndpointState existingState))
        {
            return new EndpointStateResult { State = existingState, IsNew = false };
        }

        // Slow-path: create new state with limit check
        return CreateNewEndpointState(key, shard, now);
    }

    /// <summary>
    /// Creates a new endpoint state with proper concurrency and limit enforcement.
    /// </summary>
    private EndpointStateResult CreateNewEndpointState(
        INetworkEndpoint key,
        Shard shard,
        System.Int64 now)
    {
        // Pre-check limit before allocation
        if (IsEndpointLimitReached())
        {
            _logger?.Warn($"[NW. {nameof(TokenBucketLimiter)}:Internal] endpoint-limit-reached-precheck " +
                          $"count={_totalEndpointCount} limit={_opt.MaxTrackedEndpoints}");

            return new EndpointStateResult
            {
                EarlyDecision = CreateLimitReachedDecision()
            };
        }

        System.Int32 newCount = System.Threading.Interlocked.Increment(ref _totalEndpointCount);
        EndpointState newState = CreateInitialEndpointState(now);

        if (!shard.Map.TryAdd(key, newState))
        {
            // Lost the race - another thread added it first
            _ = System.Threading.Interlocked.Decrement(ref _totalEndpointCount);
            return new EndpointStateResult
            {
                State = shard.Map[key],
                IsNew = false
            };
        }

        // Successfully added - double-check limit
        if (ShouldRejectDueToLimit(newCount))
        {
            RemoveNewlyAddedEndpoint(key, shard);
            return new EndpointStateResult
            {
                EarlyDecision = CreateLimitReachedDecision()
            };
        }

        LogNewEndpoint();
        return new EndpointStateResult { State = newState, IsNew = true };
    }

    /// <summary>
    /// Creates initial state for a new endpoint with full bucket.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private EndpointState CreateInitialEndpointState(System.Int64 now)
    {
        return new EndpointState
        {
            LastSeenSw = now,
            SoftViolations = 0,
            LastViolationSw = 0,
            HardBlockedUntilSw = 0,
            LastRefillSwTicks = now,
            MicroBalance = _initialBalanceMicro // Start with full bucket
        };
    }

    /// <summary>
    /// Checks if the endpoint tracking limit has been reached.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean IsEndpointLimitReached()
    {
        if (_opt.MaxTrackedEndpoints <= 0)
        {
            return false;
        }

        System.Int32 currentCount = System.Threading.Interlocked.CompareExchange(ref _totalEndpointCount, 0, 0);
        return currentCount >= _opt.MaxTrackedEndpoints;
    }

    /// <summary>
    /// Checks if newly added endpoint should be rejected due to limit.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean ShouldRejectDueToLimit(System.Int32 newCount) => _opt.MaxTrackedEndpoints > 0 && newCount > _opt.MaxTrackedEndpoints;

    /// <summary>
    /// Removes a newly added endpoint that exceeded the limit.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void RemoveNewlyAddedEndpoint(INetworkEndpoint key, Shard shard)
    {
        if (shard.Map.TryRemove(key, out _))
        {
            _ = System.Threading.Interlocked.Decrement(ref _totalEndpointCount);
        }
    }

    /// <summary>
    /// Creates a rate limit decision for when endpoint limit is reached.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private RateLimitDecision CreateLimitReachedDecision()
    {
        return new RateLimitDecision
        {
            Allowed = false,
            RetryAfterMs = _opt.HardLockoutSeconds * 1000,
            Credit = 0,
            Reason = RateLimitReason.HardLockout
        };
    }

    #endregion Endpoint State Management

    #region Rate Limit Evaluation

    /// <summary>
    /// Evaluates rate limit for an endpoint and returns decision.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private RateLimitDecision EvaluateRateLimit(
        INetworkEndpoint key,
        EndpointState state,
        System.Int64 now)
    {
        lock (state.Gate)
        {
            state.LastSeenSw = now;

            // Check hard lockout first
            if (IsHardBlocked(state, now, out RateLimitDecision blockedDecision))
            {
                return blockedDecision;
            }

            // Refill tokens based on elapsed time
            RefillTokens(now, state);

            // Try to consume 1 token
            if (CanConsumeToken(state))
            {
                return ConsumeTokenAndCreateDecision(state);
            }

            // Not enough tokens - handle violation
            return HandleInsufficientTokens(key, state, now);
        }
    }

    /// <summary>
    /// Checks if endpoint is currently hard-blocked.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean IsHardBlocked(
        EndpointState state,
        System.Int64 now,
        out RateLimitDecision decision)
    {
        if (state.HardBlockedUntilSw > now)
        {
            System.Int32 retryMs = CalculateDelayMs(now, state.HardBlockedUntilSw);
            _logger?.Trace($"[NW.{nameof(TokenBucketLimiter)}:Internal] hard-blocked retry_ms={retryMs}");

            decision = new RateLimitDecision
            {
                Allowed = false,
                RetryAfterMs = retryMs,
                Credit = CalculateRemainingCredit(state.MicroBalance, _opt.TokenScale),
                Reason = RateLimitReason.HardLockout
            };
            return true;
        }

        decision = default;
        return false;
    }

    /// <summary>
    /// Checks if state has enough tokens for consumption.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean CanConsumeToken(EndpointState state) => state.MicroBalance >= _opt.TokenScale;

    /// <summary>
    /// Consumes a token and creates an allowed decision.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private RateLimitDecision ConsumeTokenAndCreateDecision(EndpointState state)
    {
        state.SoftViolations = 0;
        state.MicroBalance -= _opt.TokenScale;

        System.UInt16 credit = CalculateRemainingCredit(state.MicroBalance, _opt.TokenScale);

        if (credit <= 1)
        {
            _logger?.Trace($"[NW.{nameof(TokenBucketLimiter)}:Internal] allow credit={credit}");
        }

        return new RateLimitDecision
        {
            Allowed = true,
            RetryAfterMs = 0,
            Credit = credit,
            Reason = RateLimitReason.None
        };
    }

    /// <summary>
    /// Handles the case when endpoint doesn't have enough tokens.
    /// </summary>
    private RateLimitDecision HandleInsufficientTokens(
        INetworkEndpoint key,
        EndpointState state,
        System.Int64 now)
    {
        System.Int64 needed = _opt.TokenScale - state.MicroBalance;
        System.Int32 retryMs = CalculateRetryDelayMs(needed);

        RecordViolation(state, now);

        // Check if should escalate to hard lock
        return ShouldEscalateToHardLock(state)
            ? EscalateToHardLock(key, state, now)
            : new RateLimitDecision
            {
                Allowed = false,
                RetryAfterMs = retryMs,
                Credit = CalculateRemainingCredit(state.MicroBalance, _opt.TokenScale),
                Reason = RateLimitReason.SoftThrottle
            };
    }

    /// <summary>
    /// Records a soft violation for the endpoint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void RecordViolation(EndpointState state, System.Int64 now)
    {
        System.Int64 windowTicks = ToTicks(_opt.SoftViolationWindowSeconds);

        if (now - state.LastViolationSw <= windowTicks)
        {
            state.SoftViolations++;
        }
        else
        {
            state.SoftViolations = 1;
        }

        state.LastViolationSw = now;
    }

    /// <summary>
    /// Checks if violations should escalate to hard lockout.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean ShouldEscalateToHardLock(EndpointState state) => state.SoftViolations >= _opt.MaxSoftViolations;

    /// <summary>
    /// Escalates endpoint to hard lockout.
    /// </summary>
    private RateLimitDecision EscalateToHardLock(
        INetworkEndpoint key,
        EndpointState state,
        System.Int64 now)
    {
        state.HardBlockedUntilSw = now + ToTicks(_opt.HardLockoutSeconds);
        state.SoftViolations = 0;

        System.Int32 retryMs = CalculateDelayMs(now, state.HardBlockedUntilSw);
        _logger?.Warn($"[NW.{nameof(TokenBucketLimiter)}:Internal] escalate-to-hard-lock " +
                     $"endpoint={key.Address} retry_ms={retryMs}");

        return new RateLimitDecision
        {
            Allowed = false,
            RetryAfterMs = retryMs,
            Credit = CalculateRemainingCredit(state.MicroBalance, _opt.TokenScale),
            Reason = RateLimitReason.HardLockout
        };
    }

    #endregion Rate Limit Evaluation

    #region Token Refill Logic

    /// <summary>
    /// Refills tokens based on elapsed time since last refill.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void RefillTokens(System.Int64 now, EndpointState state)
    {
        System.Int64 dt = now - state.LastRefillSwTicks;

        if (dt <= 0)
        {
            return;
        }

        // ALWAYS update to prevent dt accumulation (Bug Fix #1)
        state.LastRefillSwTicks = now;

        // Check for potential overflow
        if (dt > System.Int64.MaxValue / _refillPerSecMicro)
        {
            state.AccumulatedMicro = 0;
            state.MicroBalance = _capacityMicro;

            return;
        }

        // Only calculate refill if bucket is not already full
        if (state.MicroBalance < _capacityMicro)
        {
            // ✅ FIX: Use high-precision calculation with accumulator
            System.Int64 totalMicro = (dt * _refillPerSecMicro) + state.AccumulatedMicro;
            System.Int64 microToAdd = totalMicro / (System.Int64)_swFreq;
            System.Int64 remainder = totalMicro % (System.Int64)_swFreq;

            // Store remainder for next refill
            state.AccumulatedMicro = remainder;

            if (microToAdd > 0)
            {
                System.Int64 newBalance = state.MicroBalance + microToAdd;
                state.MicroBalance = newBalance >= _capacityMicro
                    ? _capacityMicro
                    : newBalance;

                // ✅ If capped at capacity, reset accumulator
                if (state.MicroBalance >= _capacityMicro)
                {
                    state.AccumulatedMicro = 0;
                }
            }
        }
        else
        {
            // Bucket full - reset accumulator
            state.AccumulatedMicro = 0;
        }
    }

    #endregion Token Refill Logic

    #region Time & Calculation Helpers

    /// <summary>
    /// Calculates retry delay in milliseconds for needed micro-tokens.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 CalculateRetryDelayMs(System.Int64 microNeeded)
    {
        if (_refillPerSecMicro <= 0)
        {
            return 0;
        }

        // ✅ FIX: Protect against overflow (Bug Fix #2)
        System.Double delayMs = microNeeded * 1000.0 / _refillPerSecMicro;

        if (delayMs >= MaxDelayMs)
        {
            return System.Int32.MaxValue;
        }

        System.Int32 ms = (System.Int32)System.Math.Ceiling(delayMs);
        return ms < 0 ? 0 : ms;
    }

    /// <summary>
    /// Calculates delay in milliseconds from now until target timestamp.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 CalculateDelayMs(System.Int64 now, System.Int64 untilSw)
    {
        if (untilSw <= now)
        {
            return 0;
        }

        System.Int64 dtTicks = untilSw - now;
        System.Double sec = dtTicks / _swFreq;

        // Protect against overflow
        System.Double delayMs = (sec * 1000.0) +
                               ((dtTicks - ((System.Int64)sec * (System.Int64)_swFreq)) * 1000.0 / _swFreq);

        if (delayMs >= MaxDelayMs)
        {
            return System.Int32.MaxValue;
        }

        System.Int32 ms = (System.Int32)(delayMs + 0.999); // ceil
        return ms < 0 ? 0 : ms;
    }

    /// <summary>
    /// Converts seconds to Stopwatch ticks.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int64 ToTicks(System.Int32 seconds) => (System.Int64)System.Math.Round(seconds * _swFreq);

    /// <summary>
    /// Calculates remaining whole tokens from micro balance.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 CalculateRemainingCredit(System.Int64 microBalance, System.Int32 tokenScale)
    {
        System.Int64 tokens = microBalance / tokenScale;

        return tokens <= 0 ? (System.UInt16)0 : tokens >= System.UInt16.MaxValue ? System.UInt16.MaxValue : (System.UInt16)tokens;
    }

    #endregion Time & Calculation Helpers

    #region Shard Selection

    /// <summary>
    /// Selects shard for the given endpoint using hash mixing.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Shard SelectShard(INetworkEndpoint key)
    {
        System.Int32 hash = key.GetHashCode();

        // Mix hash with FNV-1a prime for better distribution
        unchecked
        {
            System.UInt32 h = (System.UInt32)hash;
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;

            return _shards[(System.Int32)(h & (System.UInt32)(_shards.Length - 1))];
        }
    }

    #endregion Shard Selection

    #region Report Generation

    /// <summary>
    /// Collects a consistent snapshot of all endpoint states.
    /// </summary>
    private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState>>
        CollectStateSnapshot(
            System.Int64 now,
            out System.Int32 totalEndpoints,
            out System.Int32 hardBlockedCount)
    {
        System.Int32 currentCount = System.Threading.Interlocked.CompareExchange(ref _totalEndpointCount, 0, 0);
        System.Int32 estimatedCapacity = currentCount > 0 ? currentCount : (_shards.Length * 8);
        System.Int32 initialCapacity = System.Math.Max(MinReportCapacity, estimatedCapacity);

        var pool = ListPool<System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState>>.Instance;
        var snapshot = pool.Rent(minimumCapacity: initialCapacity);

        totalEndpoints = 0;
        hardBlockedCount = 0;

        // Collect snapshot from all shards
        foreach (var shard in _shards)
        {
            totalEndpoints += shard.Map.Count;

            foreach (var kv in shard.Map)
            {
                snapshot.Add(kv);

                // Count hard-blocked during collection
                System.Boolean isBlocked;
                lock (kv.Value.Gate)
                {
                    isBlocked = kv.Value.HardBlockedUntilSw > now;
                }

                if (isBlocked)
                {
                    hardBlockedCount++;
                }
            }
        }

        SortSnapshotByPressure(snapshot, now);

        return snapshot;
    }

    /// <summary>
    /// Sorts snapshot by pressure (blocked first, then by token deficit).
    /// </summary>
    private void SortSnapshotByPressure(
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState>> snapshot,
        System.Int64 now)
    {
        snapshot.Sort((a, b) =>
        {
            EndpointState sa = a.Value;
            EndpointState sb = b.Value;

            System.Boolean aBlocked, bBlocked;
            System.Int64 aMicro, bMicro;

            lock (sa.Gate)
            {
                aBlocked = sa.HardBlockedUntilSw > now;
                aMicro = sa.MicroBalance;
            }

            lock (sb.Gate)
            {
                bBlocked = sb.HardBlockedUntilSw > now;
                bMicro = sb.MicroBalance;
            }

            // Blocked endpoints first
            if (aBlocked != bBlocked)
            {
                return bBlocked.CompareTo(aBlocked);
            }

            // Then by deficit (bigger deficit = higher pressure)
            System.Int64 aDef = CalculateDeficit(aMicro);
            System.Int64 bDef = CalculateDeficit(bMicro);

            System.Int32 cmpDef = bDef.CompareTo(aDef);
            if (cmpDef != 0)
            {
                return cmpDef;
            }

            // Tie-breaker by address
            return System.String.CompareOrdinal(a.Key.Address, b.Key.Address);
        });
    }

    /// <summary>
    /// Calculates token deficit for pressure metric.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int64 CalculateDeficit(System.Int64 microBalance)
    {
        System.Int64 clamped = microBalance < 0 ? 0 :
                              (microBalance > _capacityMicro ? _capacityMicro : microBalance);
        return _capacityMicro - clamped;
    }

    /// <summary>
    /// Builds the report string from snapshot data.
    /// </summary>
    private System.String BuildReportString(
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState>> snapshot,
        System.Int32 totalEndpoints,
        System.Int32 hardBlockedCount,
        System.Int64 now)
    {
        System.Text.StringBuilder sb = new();

        AppendReportHeader(sb, totalEndpoints, hardBlockedCount);
        AppendEndpointDetails(sb, snapshot, now);

        return sb.ToString();
    }

    /// <summary>
    /// Appends report header with configuration and statistics.
    /// </summary>
    private void AppendReportHeader(
        System.Text.StringBuilder sb,
        System.Int32 totalEndpoints,
        System.Int32 hardBlockedCount)
    {
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TokenBucketLimiter Status:");
        _ = sb.AppendLine($"CapacityTokens      :  {_opt.CapacityTokens}");
        _ = sb.AppendLine($"RefillPerSecond     : {_opt.RefillTokensPerSecond}");
        _ = sb.AppendLine($"TokenScale          : {_opt.TokenScale}");
        _ = sb.AppendLine($"Shards              : {_opt.ShardCount}");
        _ = sb.AppendLine($"HardLockoutSeconds  : {_opt.HardLockoutSeconds}");
        _ = sb.AppendLine($"StaleEntrySeconds   : {_opt.StaleEntrySeconds}");
        _ = sb.AppendLine($"CleanupIntervalSecs : {_opt.CleanupIntervalSeconds}");
        _ = sb.AppendLine($"MaxTrackedEndpoints : {_opt.MaxTrackedEndpoints}");
        _ = sb.AppendLine($"TrackedEndpoints    : {totalEndpoints}");
        _ = sb.AppendLine($"HardBlockedCount    : {hardBlockedCount}");
        _ = sb.AppendLine();
    }

    /// <summary>
    /// Appends detailed endpoint information to report.
    /// </summary>
    private void AppendEndpointDetails(
        System.Text.StringBuilder sb,
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState>> snapshot,
        System.Int64 now)
    {
        _ = sb.AppendLine("Top Endpoints by Pressure:");
        _ = sb.AppendLine("-------------------------------------------------------------------------------");
        _ = sb.AppendLine("Endpoint(Key)    | Blocked | Credit | MicroBalance/Capacity | RetryAfter(ms)");
        _ = sb.AppendLine("-------------------------------------------------------------------------------");

        if (snapshot.Count == 0)
        {
            _ = sb.AppendLine("(no endpoints tracked)");
        }
        else
        {
            AppendTopEndpoints(sb, snapshot, now, maxCount: 20);
        }

        _ = sb.AppendLine("-------------------------------------------------------------------------------");
    }

    /// <summary>
    /// Appends top N endpoints to the report.
    /// </summary>
    private void AppendTopEndpoints(
        System.Text.StringBuilder sb,
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState>> snapshot,
        System.Int64 now,
        System.Int32 maxCount)
    {
        System.Int32 shown = 0;

        foreach (var kv in snapshot)
        {
            if (shown++ >= maxCount)
            {
                break;
            }

            AppendEndpointRow(sb, kv.Key, kv.Value, now);
        }
    }

    /// <summary>
    /// Appends a single endpoint row to the report.
    /// </summary>
    private void AppendEndpointRow(
        System.Text.StringBuilder sb,
        INetworkEndpoint key,
        EndpointState state,
        System.Int64 now)
    {
        System.Int64 micro, blockedUntil;

        lock (state.Gate)
        {
            micro = state.MicroBalance;
            blockedUntil = state.HardBlockedUntilSw;
        }

        System.Boolean isBlocked = blockedUntil > now;
        System.UInt16 credit = CalculateRemainingCredit(micro, _opt.TokenScale);
        System.Int32 retryMs = CalculateRetryForReport(micro, isBlocked, blockedUntil, now);

        System.String keyCol = FormatEndpointKey(key.Address);
        System.String blockedCol = isBlocked ? "yes" : " no ";

        _ = sb.AppendLine($"{keyCol} | {blockedCol}   | {credit,6} | {micro,10}/{_capacityMicro,-10} | {retryMs,12}");
    }

    /// <summary>
    /// Calculates retry time for report display.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 CalculateRetryForReport(
        System.Int64 micro,
        System.Boolean isBlocked,
        System.Int64 blockedUntil,
        System.Int64 now)
    {
        if (isBlocked)
        {
            return CalculateDelayMs(now, blockedUntil);
        }

        System.Int64 needed = (micro >= _opt.TokenScale) ? 0 : (_opt.TokenScale - micro);
        return needed > 0 ? CalculateRetryDelayMs(needed) : 0;
    }

    /// <summary>
    /// Formats endpoint key for display (truncates if too long).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String FormatEndpointKey(System.String address)
    {
        const System.Int32 MaxLength = 15;
        return address.Length > MaxLength
            ? (address[..MaxLength] + "…")
            : address.PadRight(MaxLength);
    }

    /// <summary>
    /// Returns snapshot list to pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ReturnSnapshotToPool(
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState>> snapshot)
    {
        var pool = ListPool<System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState>>.Instance;
        pool.Return(snapshot, clearItems: true);
    }

    #endregion Report Generation

    #region Cleanup

    /// <summary>
    /// Periodic cleanup of stale endpoints to bound memory use.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void CleanupStaleEndpoints()
    {
        if (_disposed)
        {
            return;
        }

        using System.Threading.CancellationTokenSource cts = new(System.TimeSpan.FromSeconds(5));
        System.Threading.CancellationToken token = cts.Token;

        try
        {
            System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();
            System.Int32 removed = PerformStaleCleanup(now, token);

            removed += EnforceLimitIfNeeded(token);

            LogCleanupResult(removed);
        }
        catch (System.OperationCanceledException)
        {
            _logger?.Warn($"[NW. {nameof(TokenBucketLimiter)}:Internal] Cleanup was cancelled due to timeout");
        }
        catch (System.Exception ex) when (ex is not System.ObjectDisposedException)
        {
            _logger?.Error($"[NW.{nameof(TokenBucketLimiter)}:Internal] cleanup-error msg={ex.Message}");
        }
    }

    /// <summary>
    /// Performs cleanup of stale endpoints across all shards.
    /// </summary>
    private System.Int32 PerformStaleCleanup(
        System.Int64 now,
        System.Threading.CancellationToken token)
    {
        System.Int32 removed = 0;
        System.Int32 visited = 0;
        System.Int64 staleTicks = ToTicks(_opt.StaleEntrySeconds);

        foreach (Shard shard in _shards)
        {
            token.ThrowIfCancellationRequested();

            foreach (var kv in shard.Map)
            {
                visited++;

                if ((visited & (CancellationCheckFrequency - 1)) == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                if (TryRemoveStaleEndpoint(kv, now, staleTicks, shard))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    /// <summary>
    /// Attempts to remove a stale endpoint with double-check pattern.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean TryRemoveStaleEndpoint(
        System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState> kv,
        System.Int64 now, System.Int64 staleTicks, Shard shard)
    {
        System.Boolean shouldRemove;
        EndpointState state = kv.Value;

        if (now - state.LastSeenSw <= staleTicks)
        {
            return false;
        }

        lock (state.Gate)
        {
            shouldRemove = (now - state.LastSeenSw) > staleTicks;
        }

        if (!shouldRemove)
        {
            return false;
        }

        // Only proceed if truly stale
        if (shard.Map.TryRemove(kv.Key, out _))
        {
            _ = System.Threading.Interlocked.Decrement(ref _totalEndpointCount);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Enforces MaxTrackedEndpoints limit if exceeded.
    /// </summary>
    private System.Int32 EnforceLimitIfNeeded(System.Threading.CancellationToken token)
    {
        if (_opt.MaxTrackedEndpoints <= 0)
        {
            return 0;
        }

        System.Int32 currentCount = System.Threading.Interlocked.CompareExchange(ref _totalEndpointCount, 0, 0);

        if (currentCount <= _opt.MaxTrackedEndpoints)
        {
            return 0;
        }

        System.Int32 toRemove = currentCount - _opt.MaxTrackedEndpoints;
        System.Int32 removed = RemoveOldestEndpoints(toRemove, token);

        if (removed > 0)
        {
            _logger?.Warn($"[NW.{nameof(TokenBucketLimiter)}:Internal] " +
                         $"Evicted {removed} endpoints to enforce MaxTrackedEndpoints limit");
        }

        return removed;
    }

    /// <summary>
    /// Evicts the oldest endpoints across all shards.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Int32 RemoveOldestEndpoints(
        System.Int32 count,
        System.Threading.CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return 0;
        }

        var candidates = CollectEvictionCandidates(cancellationToken);

        try
        {
            candidates.Sort((a, b) => a.LastSeen.CompareTo(b.LastSeen));
            return EvictOldestCandidates(candidates, count, cancellationToken);
        }
        finally
        {
            ReturnEvictionCandidatesToPool(candidates);
        }
    }

    /// <summary>
    /// Collects all endpoints as eviction candidates.
    /// </summary>
    private System.Collections.Generic.List<(INetworkEndpoint Key, System.Int64 LastSeen)>
        CollectEvictionCandidates(System.Threading.CancellationToken cancellationToken)
    {
        System.Int32 estimatedCapacity = System.Math.Min(
            _totalEndpointCount * 2,
            MaxEvictionCapacity);

        var pool = ListPool<(INetworkEndpoint Key, System.Int64 LastSeen)>.Instance;
        var candidates = pool.Rent(minimumCapacity: estimatedCapacity);

        foreach (Shard shard in _shards)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var kv in shard.Map)
            {
                System.Int64 lastSeen;
                lock (kv.Value.Gate)
                {
                    lastSeen = kv.Value.LastSeenSw;
                }
                candidates.Add((kv.Key, lastSeen));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Evicts the oldest N candidates.
    /// </summary>
    private System.Int32 EvictOldestCandidates(
        System.Collections.Generic.List<(INetworkEndpoint Key, System.Int64 LastSeen)> candidates,
        System.Int32 count,
        System.Threading.CancellationToken cancellationToken)
    {
        System.Int32 removed = 0;
        System.Int32 toRemove = System.Math.Min(count, candidates.Count);

        for (System.Int32 i = 0; i < toRemove; i++)
        {
            if ((i & (CancellationCheckFrequency - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var endpoint = candidates[i].Key;
            Shard shard = SelectShard(endpoint);

            if (shard.Map.TryRemove(endpoint, out _))
            {
                removed++;
                _ = System.Threading.Interlocked.Decrement(ref _totalEndpointCount);
            }
        }

        return removed;
    }

    /// <summary>
    /// Returns eviction candidates list to pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ReturnEvictionCandidatesToPool(
        System.Collections.Generic.List<(INetworkEndpoint Key, System.Int64 LastSeen)> candidates)
    {
        var pool = ListPool<(INetworkEndpoint Key, System.Int64 LastSeen)>.Instance;
        pool.Return(candidates, clearItems: true);
    }

    #endregion Cleanup

    #region Logging

    /// <summary>
    /// Logs initialization parameters.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void LogInitialization()
    {
        System.String initialDesc = _opt.InitialTokens < 0
            ? "full"
            : _opt.InitialTokens.ToString();

        _logger?.Debug($"[NW. {nameof(TokenBucketLimiter)}] init " +
                       $"initial={initialDesc} " +
                       $"scale={_opt.TokenScale} " +
                       $"shards={_opt.ShardCount} " +
                       $"cap={_opt.CapacityTokens} " +
                       $"stale_s={_opt.StaleEntrySeconds} " +
                       $"hardlock_s={_opt.HardLockoutSeconds} " +
                       $"refill={_opt.RefillTokensPerSecond}/s " +
                       $"cleanup_s={_opt.CleanupIntervalSeconds} " +
                       $"max_endpoints={_opt.MaxTrackedEndpoints}");
    }

    /// <summary>
    /// Logs creation of new endpoint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void LogNewEndpoint()
    {
        _logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}:Internal] " +
                       $"new-endpoint total={_totalEndpointCount}");
    }

    /// <summary>
    /// Logs cleanup result if any endpoints were removed.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void LogCleanupResult(System.Int32 removed)
    {
        if (removed > 0)
        {
            _logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}:Internal] " +
                           $"Cleanup removed={removed}");
        }
    }

    #endregion Logging

    #region Initialization

    /// <summary>
    /// Schedules the recurring cleanup job.
    /// </summary>
    private void ScheduleCleanupJob()
    {
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: TaskNaming.Recurring.CleanupJobId(nameof(TokenBucketLimiter), this.GetHashCode()),
            interval: System.TimeSpan.FromSeconds(_cleanupIntervalSec),
            work: _ =>
            {
                this.CleanupStaleEndpoints();
                return System.Threading.Tasks.ValueTask.CompletedTask;
            },
            options: new RecurringOptions
            {
                Tag = nameof(TokenBucketLimiter),
                NonReentrant = true,
                Jitter = System.TimeSpan.FromMilliseconds(250),
                ExecutionTimeout = System.TimeSpan.FromSeconds(2),
                BackoffCap = System.TimeSpan.FromSeconds(15)
            }
        );
    }

    /// <summary>
    /// Calculates the initial micro-token balance for new endpoints based on configuration.
    /// </summary>
    /// <returns>Initial balance in micro-tokens. </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int64 CalculateInitialBalance()
    {
        // Default (-1): Start with full capacity
        if (_opt.InitialTokens < 0)
        {
            return _capacityMicro;
        }

        // Explicit 0: Start empty (cold-start mode)
        if (_opt.InitialTokens == 0)
        {
            return 0;
        }

        // Custom value:  Clamp to [0, capacity]
        System.Int64 requestedMicro = (System.Int64)_opt.InitialTokens * _opt.TokenScale;
        return System.Math.Clamp(requestedMicro, 0, _capacityMicro);
    }

    #endregion Initialization

    #region IDisposable & IAsyncDisposable

    /// <inheritdoc />
    /// <summary>
    /// Synchronously disposes the limiter and cancels cleanup job.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        InstanceManager.Instance.GetOrCreateInstance<TaskManager>()?
                                .CancelRecurring(TaskNaming.Recurring
                                .CleanupJobId(nameof(TokenBucketLimiter), this
                                .GetHashCode()));

        _logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}:{nameof(Dispose)}] disposed");
    }

    /// <inheritdoc />
    /// <summary>
    /// Asynchronously disposes the limiter (delegates to sync Dispose).
    /// </summary>
    public System.Threading.Tasks.ValueTask DisposeAsync()
    {
        Dispose();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    #endregion IDisposable & IAsyncDisposable
}