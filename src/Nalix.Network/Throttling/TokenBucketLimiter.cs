// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Logging;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Configurations;

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
    public readonly struct LimitDecision
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
        /// <summary>
        /// NONE.
        /// </summary>
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
        public System.Int64 LastRefillSwTicks;
        public System.Int64 MicroBalance;         // fixed-point: tokens * TokenScale
        public System.Int64 HardBlockedUntilSw;   // 0 if not hard-blocked
        public System.Int64 LastSeenSw;           // for cleanup
        public System.Int32 SoftViolations;
        public System.Int64 LastViolationSw;
    }

    /// <summary>A shard contains a dictionary of endpoint states and a shard-level lock for map mutation.</summary>
    private sealed class Shard
    {
        public readonly System.Collections.Concurrent.ConcurrentDictionary<INetworkEndpoint, EndpointState> Map = new();

        // No shard-wide lock necessary for map ops; per-key Gate handles mutation.
    }

    #endregion Private Types

    #region Fields

    private readonly System.Int32 _cleanupIntervalSec;
    private readonly TokenBucketOptions _opt;
    private readonly System.Int64 _capacityMicro;
    private readonly System.Int64 _refillPerSecMicro;
    private readonly System.Double _swFreq; // Stopwatch ticks per second
    private readonly System.Double _microPerTick; // NEW: micro-tokens per tick

    private readonly Shard[] _shards;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private readonly ILogger _logger;

    private System.Boolean _disposed;
    private System.Int32 _totalEndpointCount; // Track total endpoints across all shards

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Creates a new TokenBucketLimiter with provided options.
    /// </summary>
    public TokenBucketLimiter([System.Diagnostics.CodeAnalysis.AllowNull] TokenBucketOptions options = null)
    {
        _opt = options ?? ConfigurationManager.Instance.Get<TokenBucketOptions>();
        ValidateOptions(_opt);

        _capacityMicro = (System.Int64)_opt.CapacityTokens * _opt.TokenScale;
        _refillPerSecMicro = (System.Int64)System.Math.Round(_opt.RefillTokensPerSecond * _opt.TokenScale);
        _swFreq = System.Diagnostics.Stopwatch.Frequency;
        _microPerTick = _refillPerSecMicro / _swFreq;

        _shards = new Shard[_opt.ShardCount];
        for (var i = 0; i < _shards.Length; i++)
        {
            _shards[i] = new Shard();
        }

        _cleanupIntervalSec = _opt.CleanupIntervalSeconds;
        _totalEndpointCount = 0;
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: TaskNaming.Recurring.CleanupJobId(nameof(TokenBucketLimiter), this.GetHashCode()),
            interval: System.TimeSpan.FromSeconds(this._cleanupIntervalSec),
            work: _ =>
            {
                this.Cleanup();
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

        _logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}] init cap={_opt.CapacityTokens} " +
                       $"refill={_opt.RefillTokensPerSecond}/s scale={_opt.TokenScale} " +
                       $"shards={_opt.ShardCount} stale_s={_opt.StaleEntrySeconds} " +
                       $"cleanup_s={_opt.CleanupIntervalSeconds} hardlock_s={_opt.HardLockoutSeconds} " +
                       $"max_endpoints={_opt.MaxTrackedEndpoints}");

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenBucketLimiter"/> class with default options.
    /// </summary>
    public TokenBucketLimiter() : this(null) { }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Checks and consumes 1 token for the given endpoint. Returns decision with RetryAfter and Credit.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    internal LimitDecision Check([System.Diagnostics.CodeAnalysis.NotNull] INetworkEndpoint key)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(TokenBucketLimiter));

        if (key is null)
        {
            throw new System.ArgumentNullException(nameof(key), "Endpoint cannot be null");
        }

        if (System.String.IsNullOrEmpty(key.Address))
        {
            throw new System.ArgumentException("Endpoint address cannot be null or empty", nameof(key));
        }

        System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();
        Shard shard = GetShard(key);
        System.Collections.Concurrent.ConcurrentDictionary<INetworkEndpoint, EndpointState> map = shard.Map;

        System.Boolean isNew = false;

        // Fast-path: endpoint already tracked
        if (!map.TryGetValue(key, out EndpointState state))
        {
            // Check if we've reached the endpoint limit before creating new state
            if (_opt.MaxTrackedEndpoints > 0)
            {
                System.Int32 currentCount = System.Threading.Interlocked.CompareExchange(ref _totalEndpointCount, 0, 0);
                if (currentCount >= _opt.MaxTrackedEndpoints)
                {
                    _logger?.Warn($"[NW.{nameof(TokenBucketLimiter)}:Internal] endpoint-limit-reached count={currentCount} limit={_opt.MaxTrackedEndpoints}");
                    
                    // Return a hard lockout decision for new endpoints when limit reached
                    return new LimitDecision
                    {
                        Allowed = false,
                        RetryAfterMs = _opt.HardLockoutSeconds * 1000,
                        Credit = 0,
                        Reason = RateLimitReason.HardLockout
                    };
                }
            }

            // Slow-path: create new state
            state = new EndpointState
            {
                LastRefillSwTicks = now,
                MicroBalance = 0,
                HardBlockedUntilSw = 0,
                LastSeenSw = now
            };

            if (!map.TryAdd(key, state))
            {
                // Lost the race, use existing one
                state = map[key];
            }
            else
            {
                isNew = true;
                _ = System.Threading.Interlocked.Increment(ref _totalEndpointCount);
            }
        }

        if (isNew)
        {
            _logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}:Internal] new-endpoint total={_totalEndpointCount}");
        }

        lock (state.Gate)
        {
            state.LastSeenSw = now;

            // Hard lockout?
            if (state.HardBlockedUntilSw > now)
            {
                System.Int32 retryMsHard = ComputeMs(now, state.HardBlockedUntilSw);
                _logger?.Trace($"[NW.{nameof(TokenBucketLimiter)}:Internal] hard-blocked retry_ms={retryMsHard}");

                return new LimitDecision
                {
                    Allowed = false,
                    RetryAfterMs = retryMsHard,
                    Credit = GetCredit(state.MicroBalance, _opt.TokenScale),
                    Reason = RateLimitReason.HardLockout
                };
            }

            // Refill micro-tokens
            Refill(now, state);

            // If enough for 1 token (TokenScale micro), consume and allow
            if (state.MicroBalance >= _opt.TokenScale)
            {
                state.SoftViolations = 0;
                state.MicroBalance -= _opt.TokenScale;
                System.UInt16 credit = GetCredit(state.MicroBalance, _opt.TokenScale);

                if (credit <= 1)
                {
                    _logger?.Trace($"[NW.{nameof(TokenBucketLimiter)}:Internal] allow credit={credit}");
                }

                return new LimitDecision
                {
                    Allowed = true,
                    RetryAfterMs = 0,
                    Credit = credit,
                    Reason = RateLimitReason.None
                };
            }

            // Not enough: compute soft retry-after
            System.Int64 needed = _opt.TokenScale - state.MicroBalance;
            System.Int32 retryMs = ComputeRetryMsForMicro(needed);
            System.Int64 windowTicks = ToSwTicks(_opt.SoftViolationWindowSeconds);

            if (now - state.LastViolationSw <= windowTicks)
            {
                state.SoftViolations++;
            }
            else
            {
                state.SoftViolations = 1;
            }

            state.LastViolationSw = now;

            // Escalate to hard lock
            if (state.SoftViolations >= _opt.MaxSoftViolations)
            {
                state.HardBlockedUntilSw = now + ToSwTicks(_opt.HardLockoutSeconds);
                state.SoftViolations = 0;

                return new LimitDecision
                {
                    Allowed = false,
                    RetryAfterMs = ComputeMs(now, state.HardBlockedUntilSw),
                    Credit = GetCredit(state.MicroBalance, _opt.TokenScale),
                    Reason = RateLimitReason.HardLockout
                };
            }


            return new LimitDecision
            {
                Allowed = false,
                RetryAfterMs = ComputeRetryMsForMicro(needed),
                Credit = GetCredit(state.MicroBalance, _opt.TokenScale),
                Reason = RateLimitReason.SoftThrottle
            };
        }
    }

    /// <summary>
    /// Generates a human-readable diagnostic report of the limiter state.
    /// Includes config overview, shard stats, and top endpoints by pressure.
    /// </summary>
    /// <returns>Formatted string report.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public System.String GenerateReport()
    {
        System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();
        System.Int32 shardCount = _shards.Length;
        System.Int32 totalEndpoints = 0;
        System.Int32 hardBlockedCount = 0;

        // Use ListPool to reduce allocations - rent a list for snapshot
        var pool = Nalix.Shared.Memory.Pools.ListPool<
            System.Collections.Generic.KeyValuePair<INetworkEndpoint, EndpointState>>.Instance;
        var snapshot = pool.Rent(minimumCapacity: 256);

        try
        {
            // Collect a consistent snapshot
            for (System.Int32 i = 0; i < shardCount; i++)
        {
            var map = _shards[i].Map;
            totalEndpoints += map.Count;

            foreach (var kv in map)
            {
                // We only need a reference to the EndpointState for later read under lock
                snapshot.Add(kv);
            }
        }

        // Compute a “pressure” metric for sorting:
        // - Prefer hard-blocked endpoints first
        // - Then sort by token deficit (bigger deficit = heavier pressure)
        // - Stable tie-breaker by endpoint hash
        snapshot.Sort((a, b) =>
        {
            var sa = a.Value;
            var sb = b.Value;

            System.Boolean aBlocked = sa.HardBlockedUntilSw > now;
            System.Boolean bBlocked = sb.HardBlockedUntilSw > now;
            if (aBlocked != bBlocked)
            {
                return bBlocked.CompareTo(aBlocked); // blocked first
            }

            // Estimate micro-deficit = capacity - clamp(micro, [0, capacity])
            System.Int64 aMicro, bMicro;
            lock (sa.Gate)
            {
                aMicro = sa.MicroBalance;
            }

            lock (sb.Gate)
            {
                bMicro = sb.MicroBalance;
            }

            System.Int64 aDef = _capacityMicro - (aMicro < 0 ? 0 : (aMicro > _capacityMicro ? _capacityMicro : aMicro));
            System.Int64 bDef = _capacityMicro - (bMicro < 0 ? 0 : (bMicro > _capacityMicro ? _capacityMicro : bMicro));

            System.Int32 cmpDef = bDef.CompareTo(aDef);
            if (cmpDef != 0)
            {
                return cmpDef;
            }

            // Fallback: ordinal string compare (cheap enough)
            return System.String.CompareOrdinal(a.Key.Address, b.Key.Address);
        });

        // Count hard-blocked after sort pass
        foreach (var kv in snapshot)
        {
            if (kv.Value.HardBlockedUntilSw > now)
            {
                hardBlockedCount++;
            }
        }

        // Build report
        System.Text.StringBuilder sb = new();
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TokenBucketLimiter Status:");
        _ = sb.AppendLine($"CapacityTokens      : {this._opt.CapacityTokens}");
        _ = sb.AppendLine($"RefillPerSecond     : {this._opt.RefillTokensPerSecond}");
        _ = sb.AppendLine($"TokenScale          : {this._opt.TokenScale}");
        _ = sb.AppendLine($"Shards              : {this._opt.ShardCount}");
        _ = sb.AppendLine($"HardLockoutSeconds  : {this._opt.HardLockoutSeconds}");
        _ = sb.AppendLine($"StaleEntrySeconds   : {this._opt.StaleEntrySeconds}");
        _ = sb.AppendLine($"CleanupIntervalSecs : {this._opt.CleanupIntervalSeconds}");
        _ = sb.AppendLine($"MaxTrackedEndpoints : {this._opt.MaxTrackedEndpoints}");
        _ = sb.AppendLine($"TrackedEndpoints    : {totalEndpoints}");
        _ = sb.AppendLine($"HardBlockedCount    : {hardBlockedCount}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Top Endpoints by Pressure:");
        _ = sb.AppendLine("-------------------------------------------------------------------------------");
        _ = sb.AppendLine("Endpoint(Key)    | Blocked | Credit | MicroBalance/Capacity | RetryAfter(ms)");
        _ = sb.AppendLine("-------------------------------------------------------------------------------");

        System.Int32 shown = 0;
        foreach (var kv in snapshot)
        {
            if (shown++ >= 20)
            {
                break;
            }

            var key = kv.Key;
            var st = kv.Value;

            System.Int64 micro;
            System.Int64 blockedUntil;
            System.Int64 lastRefill;
            lock (st.Gate)
            {
                micro = st.MicroBalance;
                blockedUntil = st.HardBlockedUntilSw;
                lastRefill = st.LastRefillSwTicks; // not printed, but could be useful
            }

            System.Boolean isBlocked = blockedUntil > now;
            // Remaining whole tokens (credit) derived from micro balance.
            System.UInt16 credit = GetCredit(micro, _opt.TokenScale);

            // If currently blocked, how long until unblocked?
            System.Int32 retryMs = 0;
            if (isBlocked)
            {
                retryMs = ComputeMs(now, blockedUntil);
            }
            else
            {
                // If not blocked but not enough for 1 token, estimate soft retry for 1 token.
                System.Int64 needed = (micro >= _opt.TokenScale) ? 0 : (_opt.TokenScale - micro);
                if (needed > 0)
                {
                    retryMs = ComputeRetryMsForMicro(needed);
                }
            }

            System.String keyCol = key.Address.Length > 15 ? (key.Address[..15] + "…") : key.Address.PadRight(15);
            _ = sb.AppendLine(
                $"{keyCol} | {(isBlocked ? "yes" : " no ")}   | {credit,6} | {micro,10}/{this._capacityMicro,-10} | {retryMs,12}");
        }

        if (shown == 0)
        {
            _ = sb.AppendLine("(no endpoints tracked)");
        }

        _ = sb.AppendLine("-------------------------------------------------------------------------------");
        return sb.ToString();
        }
        finally
        {
            // Return the list to the pool to reduce allocations
            pool.Return(snapshot, clearItems: true);
        }
    }

    #endregion Public API

    #region Private Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Shard GetShard(INetworkEndpoint key)
    {
        // Deterministic hashing; simple but fast. You can replace with XxHash if needed.
        System.Int32 h = key.GetHashCode();
        unchecked
        {
            System.UInt32 uh = (System.UInt32)h;
            return _shards[(System.Int32)(uh & (System.UInt32)(_shards.Length - 1))];
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void Refill(System.Int64 now, EndpointState state)
    {
        System.Int64 dt = now - state.LastRefillSwTicks;
        if (dt <= 0)
        {
            return;
        }

        if (dt > System.Int64.MaxValue / _refillPerSecMicro)
        {
            // Cap the refill to maximum capacity
            state.MicroBalance = _capacityMicro;
            state.LastRefillSwTicks = now;
            return;
        }

        // Add (dt / sec) * refillPerSecMicro, using integer math
        // microToAdd = (dt * refillPerSecMicro) / StopwatchFrequency
        System.Int64 microToAdd = (state.MicroBalance < _capacityMicro)
            ? dt * _refillPerSecMicro / (System.Int64)_swFreq
            : 0;

        if (microToAdd > 0)
        {
            System.Int64 nb = state.MicroBalance + microToAdd;
            state.MicroBalance = nb >= _capacityMicro ? _capacityMicro : nb;
            state.LastRefillSwTicks = now;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 ComputeRetryMsForMicro(System.Int64 microNeeded)
    {
        if (_refillPerSecMicro <= 0)
        {
            return 0; // no soft backoff possible (degenerate config)
        }
        // timeSeconds = microNeeded / refillPerSecMicro
        // ms = ceil(seconds * 1000)
        var ms = (System.Int32)System.Math.Ceiling(microNeeded * 1000.0 / _refillPerSecMicro);
        return ms < 0 ? 0 : ms;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 ComputeMs(System.Int64 now, System.Int64 untilSw)
    {
        if (untilSw <= now)
        {
            return 0;
        }

        var dtTicks = untilSw - now;
        var sec = dtTicks / _swFreq;
        var fracTicks = dtTicks - ((System.Int64)sec * (System.Int64)_swFreq);
        var ms = (System.Int32)((sec * 1000.0) + (fracTicks * 1000.0 / _swFreq) + 0.999); // ceil
        return ms < 0 ? 0 : ms;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int64 ToSwTicks(System.Int32 seconds) => (System.Int64)System.Math.Round(seconds * _swFreq);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 GetCredit(System.Int64 microBalance, System.Int32 tokenScale)
    {
        System.Int64 t = microBalance / tokenScale;
        return t <= 0 ? (System.UInt16)0 : t >= System.UInt16.MaxValue ? System.UInt16.MaxValue : (System.UInt16)t;
    }

    private static void ValidateOptions(TokenBucketOptions opt)
    {
        if (opt.CapacityTokens <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.CapacityTokens)} must be > 0, got {opt.CapacityTokens}");
        }

        if (opt.RefillTokensPerSecond <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.RefillTokensPerSecond)} must be > 0, got {opt.RefillTokensPerSecond}");
        }

        if (opt.TokenScale <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.TokenScale)} must be > 0, got {opt.TokenScale}");
        }

        // ShardCount must be power-of-two for bit-mask; adjust if needed.
        if (opt.ShardCount <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.ShardCount)} must be > 0, got {opt.ShardCount}");
        }

        if ((opt.ShardCount & (opt.ShardCount - 1)) != 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.ShardCount)} must be a power of two (e.g., 32, 64), got {opt.ShardCount}");
        }

        if (opt.StaleEntrySeconds <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.StaleEntrySeconds)} must be > 0, got {opt.StaleEntrySeconds}");
        }

        if (opt.CleanupIntervalSeconds <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.CleanupIntervalSeconds)} must be > 0, got {opt.CleanupIntervalSeconds}");
        }

        if (opt.MaxTrackedEndpoints < 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.MaxTrackedEndpoints)} must be >= 0, got {opt.MaxTrackedEndpoints}");
        }

        if (opt.HardLockoutSeconds < 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.HardLockoutSeconds)} must be >= 0, got {opt.HardLockoutSeconds}");
        }

        if (opt.SoftViolationWindowSeconds <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.SoftViolationWindowSeconds)} must be > 0, got {opt.SoftViolationWindowSeconds}");
        }

        if (opt.MaxSoftViolations <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.MaxSoftViolations)} must be > 0, got {opt.MaxSoftViolations}");
        }
    }

    #endregion Private Helpers

    #region Cleanup

    /// <summary>
    /// Periodic cleanup of stale endpoints to bound memory use.
    /// Also enforces MaxTrackedEndpoints limit via LRU eviction if needed.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void Cleanup()
    {
        if (_disposed)
        {
            return;
        }

        System.Threading.CancellationToken token = new System.Threading.CancellationTokenSource(System.TimeSpan.FromSeconds(5)).Token;

        try
        {
            System.Int32 removed = 0, visited = 0;
            System.Int64 staleTicks = ToSwTicks(_opt.StaleEntrySeconds);
            System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();

            // First pass: remove stale endpoints
            foreach (Shard shard in _shards)
            {
                token.ThrowIfCancellationRequested();

                foreach (var kv in shard.Map)
                {
                    visited++;

                    if ((visited & 0xFF) == 0) // Check every 256 items
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    EndpointState state = kv.Value;
                    if (now - state.LastSeenSw > staleTicks)
                    {
                        // Best-effort: try remove if still present
                        if (shard.Map.TryRemove(kv.Key, out _))
                        {
                            removed++;
                            _ = System.Threading.Interlocked.Decrement(ref _totalEndpointCount);
                        }
                    }
                }
            }

            // Second pass: enforce MaxTrackedEndpoints limit if needed
            if (_opt.MaxTrackedEndpoints > 0)
            {
                System.Int32 currentCount = System.Threading.Interlocked.CompareExchange(ref _totalEndpointCount, 0, 0);
                if (currentCount > _opt.MaxTrackedEndpoints)
                {
                    System.Int32 toRemove = currentCount - _opt.MaxTrackedEndpoints;
                    System.Int32 limitRemoved = EvictOldestEndpoints(toRemove, token);
                    removed += limitRemoved;
                    
                    if (limitRemoved > 0)
                    {
                        _logger?.Warn($"[NW.{nameof(TokenBucketLimiter)}:Internal] Evicted {limitRemoved} endpoints to enforce MaxTrackedEndpoints limit");
                    }
                }
            }

            if (removed > 0)
            {
                _logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}:Internal] Cleanup visited={visited} removed={removed}");
            }
        }
        catch (System.OperationCanceledException)
        {
            _logger?.Warn("Cleanup was cancelled due to timeout");
        }
        catch (System.Exception ex) when (ex is not System.ObjectDisposedException)
        {
            _logger?.Error($"[NW.{nameof(TokenBucketLimiter)}:Internal] cleanup-error msg={ex.Message}");
        }
    }

    /// <summary>
    /// Evicts the oldest (least recently seen) endpoints across all shards.
    /// Used to enforce MaxTrackedEndpoints limit.
    /// </summary>
    /// <param name="count">Number of endpoints to evict.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Number of endpoints actually removed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Int32 EvictOldestEndpoints(System.Int32 count, System.Threading.CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return 0;
        }

        // Use ListPool for temporary collection
        var pool = Nalix.Shared.Memory.Pools.ListPool<
            (INetworkEndpoint Key, System.Int64 LastSeen)>.Instance;
        var candidates = pool.Rent(minimumCapacity: count * 2);

        try
        {
            System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();

            // Collect all endpoints with their last seen time
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

            // Sort by LastSeen (oldest first)
            candidates.Sort((a, b) => a.LastSeen.CompareTo(b.LastSeen));

            // Remove the oldest ones up to count
            System.Int32 removed = 0;
            System.Int32 toRemove = System.Math.Min(count, candidates.Count);

            for (System.Int32 i = 0; i < toRemove; i++)
            {
                if ((i & 0xFF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var endpoint = candidates[i].Key;
                Shard shard = GetShard(endpoint);

                if (shard.Map.TryRemove(endpoint, out _))
                {
                    removed++;
                    _ = System.Threading.Interlocked.Decrement(ref _totalEndpointCount);
                }
            }

            return removed;
        }
        finally
        {
            pool.Return(candidates, clearItems: true);
        }
    }

    #endregion Cleanup

    #region IDisposable & IAsyncDisposable

    /// <inheritdoc />
    public void Dispose() => this.DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = (InstanceManager.Instance.GetOrCreateInstance<TaskManager>()?
                                     .CancelRecurring(TaskNaming.Recurring.CleanupJobId(nameof(TokenBucketLimiter), this.GetHashCode())));

        await System.Threading.Tasks.Task.Yield();

        _logger?.Debug($"[NW.{nameof(TokenBucketLimiter)}:{nameof(Dispose)}] disposed");
    }

    #endregion IDisposable & IAsyncDisposable
}
