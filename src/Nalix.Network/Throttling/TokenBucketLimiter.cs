// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Shared;
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
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed class TokenBucketLimiter : IDisposable, IAsyncDisposable, IReportable
{
    #region Public Types

    /// <summary>
    /// Decision result for a rate-limit check.
    /// </summary>
    public readonly struct RateLimitDecision
    {
        /// <summary>True if request is allowed (token consumed).</summary>
        public bool Allowed { get; init; }

        /// <summary>Milliseconds until at least 1 token becomes available (0 if allowed or no soft backoff).</summary>
        public int RetryAfterMs { get; init; }

        /// <summary>Remaining whole tokens (credit) after the check.</summary>
        public ushort Credit { get; init; }

        /// <summary>Reason for throttling; NONE if allowed.</summary>
        public RateLimitReason Reason { get; init; }
    }

    /// <summary>
    /// Throttling reason taxonomy.
    /// </summary>
    public enum RateLimitReason : byte
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
        public readonly object Gate = new();

        public long LastSeenSw;
        public long MicroBalance;
        public int SoftViolations;
        public long LastViolationSw;
        public long AccumulatedMicro;
        public long LastRefillSwTicks;
        public long HardBlockedUntilSw;
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
        public bool IsNew { get; init; }
        public RateLimitDecision? EarlyDecision { get; init; }
    }

    #endregion Private Types

    #region Constants

    private const int MinReportCapacity = 256;
    private const int MaxEvictionCapacity = 4096;
    private const int CancellationCheckFrequency = 256;
    private const double MaxDelayMs = int.MaxValue - 1000.0;

    #endregion Constants

    #region Fields

    private readonly Shard[] _shards;
    private readonly double _swFreq;
    private readonly TokenBucketOptions _options;
    private readonly long _capacityMicro;
    private readonly long _refillPerSecMicro;
    private readonly int _cleanupIntervalSec;
    private readonly long _initialBalanceMicro;

    private readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    private int _totalEndpointCount;
    private volatile bool _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Recurring job name for cleanup task. Format: "token.bucket".
    /// </summary>
    public static readonly string RecurringName;

    #endregion Properties

    #region Constructors

    static TokenBucketLimiter() => RecurringName = "token.bucket";

    /// <summary>
    /// Creates a new TokenBucketLimiter with provided options.
    /// </summary>
    /// <param name="options">Configuration options for the limiter.</param>
    /// <exception cref="InternalErrorException">Thrown when options validation fails.</exception>
    public TokenBucketLimiter(TokenBucketOptions? options = null)
    {
        _options = options ?? ConfigurationManager.Instance.Get<TokenBucketOptions>();
        _options.Validate();

        _totalEndpointCount = 0;
        _shards = new Shard[_options.ShardCount];
        _swFreq = Stopwatch.Frequency;
        _cleanupIntervalSec = _options.CleanupIntervalSeconds;
        _capacityMicro = (long)_options.CapacityTokens * _options.TokenScale;
        _refillPerSecMicro = (long)Math.Round(_options.RefillTokensPerSecond * _options.TokenScale);

        _initialBalanceMicro = CalculateInitialBalance();

        for (int i = 0; i < _shards.Length; i++)
        {
            _shards[i] = new Shard();
        }

        SCHEDULE_CLEANUP_JOB();

        string initialDesc = _options.InitialTokens < 0
            ? "full"
            : _options.InitialTokens.ToString(CultureInfo.InvariantCulture);

        s_logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}] init " +
                       $"initial={initialDesc} " +
                       $"scale={_options.TokenScale} " +
                       $"shards={_options.ShardCount} " +
                       $"cap={_options.CapacityTokens} " +
                       $"stale_s={_options.StaleEntrySeconds} " +
                       $"hardlock_s={_options.HardLockoutSeconds} " +
                       $"refill={_options.RefillTokensPerSecond}/s " +
                       $"cleanup_s={_options.CleanupIntervalSeconds} " +
                       $"max_endpoints={_options.MaxTrackedEndpoints}");
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
    /// <exception cref="ObjectDisposedException">Thrown when the limiter has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key address is null or empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public RateLimitDecision Check(INetworkEndpoint key)
    {
        if (_disposed)
        {
            return new RateLimitDecision
            {
                Allowed = false,
                RetryAfterMs = 0,
                Credit = 0,
                Reason = RateLimitReason.HardLockout
            };
        }

        VALIDATE_ENDPOINT(key);

        long now = Stopwatch.GetTimestamp();
        Shard shard = SELECT_SHARD(key);

        EndpointStateResult result = GET_OR_CREATE_ENDPOINT_STATE(key, shard, now);

        // Early exit if limit reached during creation
        return result.EarlyDecision ?? EVALUATE_RATE_LIMIT(key, result.State, now);
    }

    /// <summary>
    /// Generates a human-readable diagnostic report of the limiter state.
    /// </summary>
    /// <returns>Formatted string report.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        long now = Stopwatch.GetTimestamp();

        List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot = COLLECT_STATE_SNAPSHOT(now, out int totalEndpoints, out int hardBlockedCount);

        try
        {
            return BUILD_REPORT_STRING(snapshot, totalEndpoints, hardBlockedCount, now);
        }
        finally
        {
            RETURN_SNAPSHOT_TO_POOL(snapshot);
        }
    }

    #endregion Public API

    #region Endpoint State Management

    /// <summary>
    /// Validates the provided endpoint.
    /// </summary>
    /// <param name="key"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_ENDPOINT(INetworkEndpoint key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key), "Endpoint cannot be null");
        }

        if (string.IsNullOrEmpty(key.Address))
        {
            throw new ArgumentException("Endpoint address cannot be null or empty", nameof(key));
        }
    }

    /// <summary>
    /// Gets existing or creates new endpoint state with proper limit enforcement.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="shard"></param>
    /// <param name="now"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EndpointStateResult GET_OR_CREATE_ENDPOINT_STATE(INetworkEndpoint key, Shard shard, long now)
    {
        // Fast-path: endpoint already tracked
        if (shard.Map.TryGetValue(key, out EndpointState? existingState))
        {
            return new EndpointStateResult { State = existingState, IsNew = false };
        }

        // Slow-path: create new state with limit check
        return CREATE_NEW_ENDPOINT_STATE(key, shard, now);
    }

    /// <summary>
    /// Creates a new endpoint state with proper concurrency and limit enforcement.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="shard"></param>
    /// <param name="now"></param>
    private EndpointStateResult CREATE_NEW_ENDPOINT_STATE(
        INetworkEndpoint key,
        Shard shard,
        long now)
    {
        // Pre-check limit before allocation
        if (IS_ENDPOINT_LIMIT_REACHED())
        {
            s_logger?.Warn($"[NW.{nameof(TokenBucketLimiter)}:Internal] endpoint-limit-reached-precheck count={_totalEndpointCount} limit={_options.MaxTrackedEndpoints}");

            return new EndpointStateResult
            {
                EarlyDecision = CREATE_LIMIT_REACHED_DECISION()
            };
        }

        EndpointState newState = CREATE_INITIAL_ENDPOINT_STATE(now);
        int newCount = Interlocked.Increment(ref _totalEndpointCount);

        if (!shard.Map.TryAdd(key, newState))
        {
            // Lost the race - another thread added it first
            _ = Interlocked.Decrement(ref _totalEndpointCount);

            return new EndpointStateResult
            {
                State = shard.Map[key],
                IsNew = false
            };
        }

        // Successfully added - double-check limit
        if (SHOULD_REJECT_DUE_TO_LIMIT(newCount))
        {
            REMOVE_NEWLY_ADDED_ENDPOINT(key, shard);
            return new EndpointStateResult
            {
                EarlyDecision = CREATE_LIMIT_REACHED_DECISION()
            };
        }

        s_logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}:Internal] new-endpoint total={_totalEndpointCount}");

        return new EndpointStateResult { State = newState, IsNew = true };
    }

    /// <summary>
    /// Creates initial state for a new endpoint with full bucket.
    /// </summary>
    /// <param name="now"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EndpointState CREATE_INITIAL_ENDPOINT_STATE(long now)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IS_ENDPOINT_LIMIT_REACHED()
    {
        if (_options.MaxTrackedEndpoints <= 0)
        {
            return false;
        }

        int currentCount = Volatile.Read(ref _totalEndpointCount);
        return currentCount >= _options.MaxTrackedEndpoints;
    }

    /// <summary>
    /// Checks if newly added endpoint should be rejected due to limit.
    /// </summary>
    /// <param name="newCount"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool SHOULD_REJECT_DUE_TO_LIMIT(int newCount) => _options.MaxTrackedEndpoints > 0 && newCount > _options.MaxTrackedEndpoints;

    /// <summary>
    /// Removes a newly added endpoint that exceeded the limit.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="shard"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void REMOVE_NEWLY_ADDED_ENDPOINT(INetworkEndpoint key, Shard shard)
    {
        if (shard.Map.TryRemove(key, out _))
        {
            _ = Interlocked.Decrement(ref _totalEndpointCount);
        }
    }

    /// <summary>
    /// Creates a rate limit decision for when endpoint limit is reached.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RateLimitDecision CREATE_LIMIT_REACHED_DECISION()
    {
        return new RateLimitDecision
        {
            Allowed = false,
            RetryAfterMs = _options.HardLockoutSeconds * 1000,
            Credit = 0,
            Reason = RateLimitReason.HardLockout
        };
    }

    #endregion Endpoint State Management

    #region Rate Limit Evaluation

    /// <summary>
    /// Evaluates rate limit for an endpoint and returns decision.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="state"></param>
    /// <param name="now"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RateLimitDecision EVALUATE_RATE_LIMIT(INetworkEndpoint key, EndpointState state, long now)
    {
        lock (state.Gate)
        {
            state.LastSeenSw = now;

            // Check hard lockout first
            if (IS_HARD_BLOCKED(state, now, out RateLimitDecision blockedDecision))
            {
                return blockedDecision;
            }

            // Refill tokens based on elapsed time
            REFILL_TOKENS(now, state);

            // Try to consume 1 token
            if (CAN_CONSUME_TOKEN(state))
            {
                return CONSUME_TOKEN_AN_DCREATE_DECISION(state);
            }

            // Not enough tokens - handle violation
            return HANDLE_INSUFFICIENT_TOKENS(key, state, now);
        }
    }

    /// <summary>
    /// Checks if endpoint is currently hard-blocked.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="now"></param>
    /// <param name="decision"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IS_HARD_BLOCKED(EndpointState state, long now, out RateLimitDecision decision)
    {
        if (state.HardBlockedUntilSw > now)
        {
            int retryMs = CALCULATE_DELAY_MS(now, state.HardBlockedUntilSw);
            s_logger?.Trace($"[NW.{nameof(TokenBucketLimiter)}:Internal] hard-blocked retry_ms={retryMs}");

            decision = new RateLimitDecision
            {
                Allowed = false,
                RetryAfterMs = retryMs,
                Credit = CALCULATE_REMAINING_CREDIT(state.MicroBalance, _options.TokenScale),
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
    /// <param name="state"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CAN_CONSUME_TOKEN(EndpointState state) => state.MicroBalance >= _options.TokenScale;

    /// <summary>
    /// Consumes a token and creates an allowed decision.
    /// </summary>
    /// <param name="state"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RateLimitDecision CONSUME_TOKEN_AN_DCREATE_DECISION(EndpointState state)
    {
        state.SoftViolations = 0;
        state.MicroBalance -= _options.TokenScale;

        ushort credit = CALCULATE_REMAINING_CREDIT(state.MicroBalance, _options.TokenScale);

        if (credit <= 1)
        {
            s_logger?.Trace($"[NW.{nameof(TokenBucketLimiter)}:Internal] allow credit={credit}");
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
    /// <param name="key"></param>
    /// <param name="state"></param>
    /// <param name="now"></param>
    private RateLimitDecision HANDLE_INSUFFICIENT_TOKENS(INetworkEndpoint key, EndpointState state, long now)
    {
        long needed = _options.TokenScale - state.MicroBalance;
        int retryMs = CALCULATE_RETRY_DELAY_MS(needed);

        RECORD_VIOLATION(state, now);

        // Check if should escalate to hard lock
        return SHOULD_ESCALATE_TO_HARD_LOCK(state)
            ? ESCALATE_TO_HARD_LOCK(key, state, now)
            : new RateLimitDecision
            {
                Allowed = false,
                RetryAfterMs = retryMs,
                Credit = CALCULATE_REMAINING_CREDIT(state.MicroBalance, _options.TokenScale),
                Reason = RateLimitReason.SoftThrottle
            };
    }

    /// <summary>
    /// Records a soft violation for the endpoint.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="now"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RECORD_VIOLATION(EndpointState state, long now)
    {
        long windowTicks = TO_TICKS(_options.SoftViolationWindowSeconds);

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
    /// <param name="state"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool SHOULD_ESCALATE_TO_HARD_LOCK(EndpointState state) => state.SoftViolations >= _options.MaxSoftViolations;

    /// <summary>
    /// Escalates endpoint to hard lockout.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="state"></param>
    /// <param name="now"></param>
    private RateLimitDecision ESCALATE_TO_HARD_LOCK(
        INetworkEndpoint key,
        EndpointState state,
        long now)
    {
        state.HardBlockedUntilSw = now + TO_TICKS(_options.HardLockoutSeconds);
        state.SoftViolations = 0;

        int retryMs = CALCULATE_DELAY_MS(now, state.HardBlockedUntilSw);
        s_logger?.Warn($"[NW.{nameof(TokenBucketLimiter)}:Internal] escalate-to-hard-lock " +
                     $"endpoint={key.Address} retry_ms={retryMs}");

        return new RateLimitDecision
        {
            Allowed = false,
            RetryAfterMs = retryMs,
            Credit = CALCULATE_REMAINING_CREDIT(state.MicroBalance, _options.TokenScale),
            Reason = RateLimitReason.HardLockout
        };
    }

    #endregion Rate Limit Evaluation

    #region Token Refill Logic

    /// <summary>
    /// Refills tokens based on elapsed time since last refill.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="state"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void REFILL_TOKENS(long now, EndpointState state)
    {
        long dt = now - state.LastRefillSwTicks;

        if (dt <= 0)
        {
            return;
        }

        // This prevents time drift and ensures consistent refill intervals
        state.LastRefillSwTicks = now;

        // Check for potential overflow before multiplication
        if (dt > long.MaxValue / _refillPerSecMicro)
        {
            // Extreme case: very long dt or high refill rate → cap at full
            state.AccumulatedMicro = 0;
            state.MicroBalance = _capacityMicro;
            return;
        }

        // Early exit if already at capacity (optimization)
        if (state.MicroBalance >= _capacityMicro)
        {
            state.AccumulatedMicro = 0; // Reset accumulator when full
            return;
        }

        // Formula: (dt * refillRate) + accumulated = whole_tokens * frequency + remainder
        long totalMicro = (dt * _refillPerSecMicro) + state.AccumulatedMicro;
        long microToAdd = totalMicro / (long)_swFreq;


        // Store remainder for next refill (prevents precision loss)
        state.AccumulatedMicro = totalMicro % (long)_swFreq;

        if (microToAdd > 0)
        {
            long newBalance = state.MicroBalance + microToAdd;

            // Clamp to capacity
            state.MicroBalance = newBalance >= _capacityMicro
                ? _capacityMicro
                : newBalance;

            // Reset accumulator if capped (prevents unbounded growth)
            if (state.MicroBalance >= _capacityMicro)
            {
                state.AccumulatedMicro = 0;
            }
        }
    }

    #endregion Token Refill Logic

    #region Time & Calculation Helpers

    /// <summary>
    /// Calculates retry delay in milliseconds for needed micro-tokens.
    /// </summary>
    /// <param name="microNeeded"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_RETRY_DELAY_MS(long microNeeded)
    {
        if (_refillPerSecMicro <= 0)
        {
            return 0; // No refill configured
        }

        if (microNeeded <= 0)
        {
            return 0; // No tokens needed
        }

        // Check for potential overflow BEFORE calculation
        if (microNeeded > (long.MaxValue / 1000))
        {
            // Would overflow in multiplication
            return int.MaxValue;
        }

        // Safe calculation with double precision
        double delayMs = microNeeded * 1000.0 / _refillPerSecMicro;

        // Clamp to maximum safe value
        if (delayMs >= MaxDelayMs || double.IsInfinity(delayMs) || double.IsNaN(delayMs))
        {
            return int.MaxValue;
        }

        int ms = (int)Math.Ceiling(delayMs);

        // Clamp to [0, Int32.MaxValue]
        return ms < 0 ? 0 : ms;
    }

    /// <summary>
    /// Calculates delay in milliseconds from now until target timestamp.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="untilSw"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_DELAY_MS(long now, long untilSw)
    {
        if (untilSw <= now)
        {
            return 0; // Already passed
        }

        long dtTicks = untilSw - now;

        if (dtTicks is < 0 or > (long.MaxValue / 1000))
        {
            return int.MaxValue;
        }

        double sec = dtTicks / _swFreq;
        long wholeTicks = (long)(sec * _swFreq);
        long remainderTicks = dtTicks - wholeTicks;
        double delayMs = (sec * 1000.0) + (remainderTicks * 1000.0 / _swFreq);

        if (delayMs >= MaxDelayMs || double.IsInfinity(delayMs) || double.IsNaN(delayMs))
        {
            return int.MaxValue;
        }

        int ms = (int)Math.Ceiling(delayMs);

        return ms < 0 ? 0 : ms;
    }

    /// <summary>
    /// Converts seconds to Stopwatch ticks.
    /// </summary>
    /// <param name="seconds"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long TO_TICKS(int seconds) => (long)Math.Round(seconds * _swFreq);

    /// <summary>
    /// Calculates remaining whole tokens from micro balance.
    /// </summary>
    /// <param name="microBalance"></param>
    /// <param name="tokenScale"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort CALCULATE_REMAINING_CREDIT(long microBalance, int tokenScale)
    {
        long tokens = microBalance / tokenScale;

        if (tokens <= 0)
        {
            return 0;
        }
        else
        {
            return tokens >= ushort.MaxValue ? ushort.MaxValue : (ushort)tokens;
        }
    }

    #endregion Time & Calculation Helpers

    #region Shard Selection

    /// <summary>
    /// Selects shard for the given endpoint using hash mixing.
    /// </summary>
    /// <param name="key"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Shard SELECT_SHARD(INetworkEndpoint key)
    {
        int hash = key.GetHashCode();

        // Mix hash with FNV-1a prime for better distribution
        unchecked
        {
            uint h = (uint)hash;
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;

            return _shards[(int)(h & (uint)(_shards.Length - 1))];
        }
    }

    #endregion Shard Selection

    #region Report Generation

    /// <summary>
    /// Collects a consistent snapshot of all endpoint states.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="totalEndpoints"></param>
    /// <param name="hardBlockedCount"></param>
    private List<KeyValuePair<INetworkEndpoint, EndpointState>>
        COLLECT_STATE_SNAPSHOT(long now, out int totalEndpoints, out int hardBlockedCount)
    {
        int currentCount = Interlocked.CompareExchange(ref _totalEndpointCount, 0, 0);
        int estimatedCapacity = currentCount > 0 ? currentCount : (_shards.Length * 8);
        int initialCapacity = Math.Max(MinReportCapacity, estimatedCapacity);

        ListPool<KeyValuePair<INetworkEndpoint, EndpointState>> pool = ListPool<KeyValuePair<INetworkEndpoint, EndpointState>>.Instance;
        List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot = pool.Rent(minimumCapacity: initialCapacity);

        totalEndpoints = 0;
        hardBlockedCount = 0;

        // Collect snapshot from all shards
        foreach (Shard shard in _shards)
        {
            totalEndpoints += shard.Map.Count;

            foreach (KeyValuePair<INetworkEndpoint, EndpointState> kv in shard.Map)
            {
                snapshot.Add(kv);

                // Count hard-blocked during collection
                bool isBlocked;
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

        SORT_SNAPSHOT_BY_PRESSURE(snapshot, now);

        return snapshot;
    }

    /// <summary>
    /// Sorts snapshot by pressure (blocked first, then by token deficit).
    /// </summary>
    /// <param name="snapshot"></param>
    /// <param name="now"></param>
    private void SORT_SNAPSHOT_BY_PRESSURE(List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot, long now)
    {
        snapshot.Sort((a, b) =>
        {
            EndpointState sa = a.Value;
            EndpointState sb = b.Value;

            bool aBlocked, bBlocked;
            long aMicro, bMicro;

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
            long aDef = CALCULATE_DEFICIT(aMicro);
            long bDef = CALCULATE_DEFICIT(bMicro);

            int cmpDef = bDef.CompareTo(aDef);
            if (cmpDef != 0)
            {
                return cmpDef;
            }

            // Tie-breaker by address
            return string.CompareOrdinal(a.Key.Address, b.Key.Address);
        });
    }

    /// <summary>
    /// Calculates token deficit for pressure metric.
    /// </summary>
    /// <param name="microBalance"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long CALCULATE_DEFICIT(long microBalance)
    {
        long clamped = microBalance < 0 ? 0 : microBalance > _capacityMicro ? _capacityMicro : microBalance;
        return _capacityMicro - clamped;
    }

    /// <summary>
    /// Builds the report string from snapshot data.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <param name="totalEndpoints"></param>
    /// <param name="hardBlockedCount"></param>
    /// <param name="now"></param>
    private string BUILD_REPORT_STRING(
        List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot,
        int totalEndpoints,
        int hardBlockedCount,
        long now)
    {
        StringBuilder sb = new();

        APPEND_REPORT_HEADER(sb, totalEndpoints, hardBlockedCount);
        APPEND_ENDPOINT_DETAILS(sb, snapshot, now);

        return sb.ToString();
    }

    /// <summary>
    /// Appends report header with configuration and statistics.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="totalEndpoints"></param>
    /// <param name="hardBlockedCount"></param>
    private void APPEND_REPORT_HEADER(
        StringBuilder sb,
        int totalEndpoints,
        int hardBlockedCount)
    {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TokenBucketLimiter Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CapacityTokens      :  {_options.CapacityTokens}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RefillPerSecond     : {_options.RefillTokensPerSecond}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TokenScale          : {_options.TokenScale}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Shards              : {_options.ShardCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"HardLockoutSeconds  : {_options.HardLockoutSeconds}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"StaleEntrySeconds   : {_options.StaleEntrySeconds}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CleanupIntervalSecs : {_options.CleanupIntervalSeconds}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MaxTrackedEndpoints : {_options.MaxTrackedEndpoints}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TrackedEndpoints    : {totalEndpoints}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"HardBlockedCount    : {hardBlockedCount}");
        _ = sb.AppendLine();
    }

    /// <summary>
    /// Appends detailed endpoint information to report.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="snapshot"></param>
    /// <param name="now"></param>
    private void APPEND_ENDPOINT_DETAILS(
        StringBuilder sb,
        List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot,
        long now)
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
            APPEND_TOP_ENDPOINTS(sb, snapshot, now, maxCount: 20);
        }

        _ = sb.AppendLine("-------------------------------------------------------------------------------");
    }

    /// <summary>
    /// Appends top N endpoints to the report.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="snapshot"></param>
    /// <param name="now"></param>
    /// <param name="maxCount"></param>
    private void APPEND_TOP_ENDPOINTS(
        StringBuilder sb,
        List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot,
        long now,
        int maxCount)
    {
        int shown = 0;

        foreach (KeyValuePair<INetworkEndpoint, EndpointState> kv in snapshot)
        {
            if (shown++ >= maxCount)
            {
                break;
            }

            APPEND_ENDPOINT_ROW(sb, kv.Key, kv.Value, now);
        }
    }

    /// <summary>
    /// Appends a single endpoint row to the report.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="key"></param>
    /// <param name="state"></param>
    /// <param name="now"></param>
    private void APPEND_ENDPOINT_ROW(StringBuilder sb, INetworkEndpoint key, EndpointState state, long now)
    {
        long micro, blockedUntil;

        lock (state.Gate)
        {
            micro = state.MicroBalance;
            blockedUntil = state.HardBlockedUntilSw;
        }

        bool isBlocked = blockedUntil > now;
        ushort credit = CALCULATE_REMAINING_CREDIT(micro, _options.TokenScale);
        int retryMs = CALCULATE_RETRY_FOR_REPORT(micro, isBlocked, blockedUntil, now);

        string keyCol = FORMAT_ENDPOINT_KEY(key.Address);
        string blockedCol = isBlocked ? "yes" : " no ";

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{keyCol} | {blockedCol}   | {credit,6} | {micro,10}/{_capacityMicro,-10} | {retryMs,12}");
    }

    /// <summary>
    /// Calculates retry time for report display.
    /// </summary>
    /// <param name="micro"></param>
    /// <param name="isBlocked"></param>
    /// <param name="blockedUntil"></param>
    /// <param name="now"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_RETRY_FOR_REPORT(long micro, bool isBlocked, long blockedUntil, long now)
    {
        if (isBlocked)
        {
            return CALCULATE_DELAY_MS(now, blockedUntil);
        }

        long needed = (micro >= _options.TokenScale) ? 0 : (_options.TokenScale - micro);
        return needed > 0 ? CALCULATE_RETRY_DELAY_MS(needed) : 0;
    }

    /// <summary>
    /// Formats endpoint key for display (truncates if too long).
    /// </summary>
    /// <param name="address"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FORMAT_ENDPOINT_KEY(string address)
    {
        const int MaxLength = 15;
        return address.Length > MaxLength
            ? (address[..MaxLength] + "…")
            : address.PadRight(MaxLength);
    }

    /// <summary>
    /// Returns snapshot list to pool.
    /// </summary>
    /// <param name="snapshot"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RETURN_SNAPSHOT_TO_POOL(
        List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot)
    {
        ListPool<KeyValuePair<INetworkEndpoint, EndpointState>> pool = ListPool<KeyValuePair<INetworkEndpoint, EndpointState>>.Instance;
        pool.Return(snapshot, clearItems: true);
    }

    #endregion Report Generation

    #region Cleanup

    /// <summary>
    /// Periodic cleanup of stale endpoints to bound memory use.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CLEANUP_STALE_ENDPOINTS()
    {
        if (_disposed)
        {
            return;
        }

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        CancellationToken token = cts.Token;

        try
        {
            long now = Stopwatch.GetTimestamp();
            int removed = PERFORM_STALE_CLEANUP(now, token);

            removed += ENFORCE_LIMIT_IF_NEEDED(token);

            if (removed > 0)
            {
                s_logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}:Internal] " +
                               $"Cleanup removed={removed}");
            }
        }
        catch (OperationCanceledException)
        {
            s_logger?.Warn($"[NW.{nameof(TokenBucketLimiter)}:Internal] Cleanup was cancelled due to timeout");
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            s_logger?.Error($"[NW.{nameof(TokenBucketLimiter)}:Internal] cleanup-error msg={ex.Message}");
        }
    }

    /// <summary>
    /// Performs cleanup of stale endpoints across all shards.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="token"></param>
    private int PERFORM_STALE_CLEANUP(
        long now,
        CancellationToken token)
    {
        int removed = 0;
        int visited = 0;
        long staleTicks = TO_TICKS(_options.StaleEntrySeconds);

        foreach (Shard shard in _shards)
        {
            token.ThrowIfCancellationRequested();

            foreach (KeyValuePair<INetworkEndpoint, EndpointState> kv in shard.Map)
            {
                visited++;

                if ((visited & (CancellationCheckFrequency - 1)) == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                if (TRY_REMOVE_STALE_ENDPOINT(kv, now, staleTicks, shard))
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
    /// <param name="kv"></param>
    /// <param name="now"></param>
    /// <param name="staleTicks"></param>
    /// <param name="shard"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TRY_REMOVE_STALE_ENDPOINT(
        KeyValuePair<INetworkEndpoint, EndpointState> kv,
        long now, long staleTicks, Shard shard)
    {
        bool shouldRemove;
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
            _ = Interlocked.Decrement(ref _totalEndpointCount);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Enforces MaxTrackedEndpoints limit if exceeded.
    /// </summary>
    /// <param name="token"></param>
    private int ENFORCE_LIMIT_IF_NEEDED(CancellationToken token)
    {
        if (_options.MaxTrackedEndpoints <= 0)
        {
            return 0;
        }

        int currentCount = Interlocked.CompareExchange(ref _totalEndpointCount, 0, 0);

        if (currentCount <= _options.MaxTrackedEndpoints)
        {
            return 0;
        }

        int toRemove = currentCount - _options.MaxTrackedEndpoints;
        int removed = REMOVEO_LDEST_ENDPOINTS(toRemove, token);

        if (removed > 0)
        {
            s_logger?.Warn($"[NW.{nameof(TokenBucketLimiter)}:Internal] " +
                         $"Evicted {removed} endpoints to enforce MaxTrackedEndpoints limit");
        }

        return removed;
    }

    /// <summary>
    /// Evicts the oldest endpoints across all shards.
    /// </summary>
    /// <param name="count"></param>
    /// <param name="cancellationToken"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int REMOVEO_LDEST_ENDPOINTS(
        int count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return 0;
        }

        List<(INetworkEndpoint Key, long LastSeen)> candidates = COLLECT_EVICTION_CANDIDATES(cancellationToken);

        try
        {
            candidates.Sort((a, b) => a.LastSeen.CompareTo(b.LastSeen));
            return EVICT_OLD_ESTCANDIDATES(candidates, count, cancellationToken);
        }
        finally
        {
            RETURN_EVICTION_CANDIDATES_TO_POOL(candidates);
        }
    }

    /// <summary>
    /// Collects all endpoints as eviction candidates.
    /// </summary>
    /// <param name="cancellationToken"></param>
    private List<(INetworkEndpoint Key, long LastSeen)> COLLECT_EVICTION_CANDIDATES(CancellationToken cancellationToken)
    {
        int estimatedCapacity = Math.Min(
            _totalEndpointCount * 2,
            MaxEvictionCapacity);

        ListPool<(INetworkEndpoint Key, long LastSeen)> pool = ListPool<(INetworkEndpoint Key, long LastSeen)>.Instance;
        List<(INetworkEndpoint Key, long LastSeen)> candidates = pool.Rent(minimumCapacity: estimatedCapacity);

        foreach (Shard shard in _shards)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (KeyValuePair<INetworkEndpoint, EndpointState> kv in shard.Map)
            {
                long lastSeen;
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
    /// <param name="candidates"></param>
    /// <param name="count"></param>
    /// <param name="cancellationToken"></param>
    private int EVICT_OLD_ESTCANDIDATES(
        List<(INetworkEndpoint Key, long LastSeen)> candidates,
        int count,
        CancellationToken cancellationToken)
    {
        int removed = 0;
        int toRemove = Math.Min(count, candidates.Count);

        for (int i = 0; i < toRemove; i++)
        {
            if ((i & (CancellationCheckFrequency - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            INetworkEndpoint endpoint = candidates[i].Key;
            Shard shard = SELECT_SHARD(endpoint);

            if (shard.Map.TryRemove(endpoint, out _))
            {
                removed++;
                _ = Interlocked.Decrement(ref _totalEndpointCount);
            }
        }

        return removed;
    }

    /// <summary>
    /// Returns eviction candidates list to pool.
    /// </summary>
    /// <param name="candidates"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RETURN_EVICTION_CANDIDATES_TO_POOL(List<(INetworkEndpoint Key, long LastSeen)> candidates)
    {
        ListPool<(INetworkEndpoint Key, long LastSeen)> pool = ListPool<(INetworkEndpoint Key, long LastSeen)>.Instance;
        pool.Return(candidates, clearItems: true);
    }

    #endregion Cleanup

    #region Initialization

    /// <summary>
    /// Schedules the recurring cleanup job.
    /// </summary>
    private void SCHEDULE_CLEANUP_JOB()
    {
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: TaskNaming.Recurring.CleanupJobId(RecurringName, GetHashCode()),
            interval: TimeSpan.FromSeconds(_cleanupIntervalSec),
            work: _ =>
            {
                CLEANUP_STALE_ENDPOINTS();
                return ValueTask.CompletedTask;
            },
            options: new RecurringOptions
            {
                NonReentrant = true,
                Tag = TaskNaming.Tags.Service,
                Jitter = TimeSpan.FromMilliseconds(250),
                ExecutionTimeout = TimeSpan.FromSeconds(2),
                BackoffCap = TimeSpan.FromSeconds(15)
            }
        );
    }

    /// <summary>
    /// Calculates the initial micro-token balance for new endpoints based on configuration.
    /// </summary>
    /// <returns>Initial balance in micro-tokens. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long CalculateInitialBalance()
    {
        // Default (-1): Start with full capacity
        if (_options.InitialTokens < 0)
        {
            return _capacityMicro;
        }

        // Explicit 0: Start empty (cold-start mode)
        if (_options.InitialTokens == 0)
        {
            return 0;
        }

        // Custom value:  Clamp to [0, capacity]
        long requestedMicro = (long)_options.InitialTokens * _options.TokenScale;
        return Math.Clamp(requestedMicro, 0, _capacityMicro);
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

        _ = (InstanceManager.Instance.GetOrCreateInstance<TaskManager>()?
                                .CancelRecurring(TaskNaming.Recurring
                                .CleanupJobId(RecurringName,
                                GetHashCode())));

        s_logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}:{nameof(Dispose)}] disposed");
    }

    /// <inheritdoc />
    /// <summary>
    /// Asynchronously disposes the limiter (delegates to sync Dispose).
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion IDisposable & IAsyncDisposable
}
