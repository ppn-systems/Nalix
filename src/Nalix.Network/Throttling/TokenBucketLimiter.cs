// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Logging;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.Network.Configurations;
using Nalix.Network.Internal.Net;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Throttling;

/// <summary>
/// High-performance token-bucket based rate limiter with per-endpoint state,
/// using Stopwatch ticks for time arithmetic and fixed-point token precision.
/// Provides precise Retry-After and Credit for client backoff and flow control.
/// </summary>
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

        /// <summary>Reason for throttling; None if allowed.</summary>
        public RateLimitReason Reason { get; init; }
    }

    /// <summary>
    /// Throttling reason taxonomy.
    /// </summary>

    public enum RateLimitReason : System.Byte
    {
        /// <summary>
        /// None.
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
    }

    /// <summary>A shard contains a dictionary of endpoint states and a shard-level lock for map mutation.</summary>
    private sealed class Shard
    {
        public readonly System.Collections.Concurrent.ConcurrentDictionary<NetAddressKey, EndpointState> Map = new();

        // No shard-wide lock necessary for map ops; per-key Gate handles mutation.
    }

    #endregion Private Types

    #region Fields

    private readonly System.Int32 _cleanupIntervalSec;
    private readonly TokenBucketOptions _opt;
    private readonly System.Int64 _capacityMicro;
    private readonly System.Int64 _refillPerSecMicro;
    private readonly System.Double _swFreq; // Stopwatch ticks per second

    private readonly Shard[] _shards;

    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Creates a new TokenBucketLimiter with provided options.
    /// </summary>
    public TokenBucketLimiter(TokenBucketOptions? options = null)
    {
        _opt = options ?? ConfigurationManager.Instance.Get<TokenBucketOptions>();
        ValidateOptions(_opt);

        _capacityMicro = (System.Int64)_opt.CapacityTokens * _opt.TokenScale;
        _refillPerSecMicro = (System.Int64)System.Math.Round(_opt.RefillTokensPerSecond * _opt.TokenScale);
        _swFreq = System.Diagnostics.Stopwatch.Frequency;

        _shards = new Shard[_opt.ShardCount];
        for (var i = 0; i < _shards.Length; i++)
        {
            _shards[i] = new Shard();
        }

        _cleanupIntervalSec = _opt.CleanupIntervalSeconds;

        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: TaskNames.Recurring.WithKey(nameof(TokenBucketLimiter), this.GetHashCode()),
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

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TokenBucketLimiter)}] init cap={_opt.CapacityTokens} " +
                                       $"refill={_opt.RefillTokensPerSecond}/s scale={_opt.TokenScale} " +
                                       $"shards={_opt.ShardCount} stale_s={_opt.StaleEntrySeconds} " +
                                       $"cleanup_s={_opt.CleanupIntervalSeconds} hardlock_s={_opt.HardLockoutSeconds}");

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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal LimitDecision Check(NetAddressKey key)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(TokenBucketLimiter));

        System.Boolean isNew = false;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var shard = GetShard(key);

        var state = shard.Map.GetOrAdd(key, _ =>
        {
            isNew = true;
            return new EndpointState
            {
                LastRefillSwTicks = now,
                MicroBalance = 0,
                HardBlockedUntilSw = 0,
                LastSeenSw = now
            };
        });

        if (isNew)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TokenBucketLimiter)}] new-endpoint ep={key.Address}");
        }

        lock (state.Gate)
        {
            state.LastSeenSw = now;

            // Hard lockout?
            if (state.HardBlockedUntilSw > now)
            {
                var retryMsHard = ComputeMs(now, state.HardBlockedUntilSw);
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(TokenBucketLimiter)}] hard-blocked ep={key.Address} retry_ms={retryMsHard}");

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
                state.MicroBalance -= _opt.TokenScale;
                var credit = GetCredit(state.MicroBalance, _opt.TokenScale);
                if (credit <= 1)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(TokenBucketLimiter)}] allow ep={key.Address} credit={credit}");
                }
                return new LimitDecision { Allowed = true, RetryAfterMs = 0, Credit = credit, Reason = RateLimitReason.None };
            }

            // Not enough: compute soft retry-after
            var needed = _opt.TokenScale - state.MicroBalance;
            var retryMs = ComputeRetryMsForMicro(needed);

            // Optional: set hard block window
            if (_opt.HardLockoutSeconds > 0)
            {
                state.HardBlockedUntilSw = now + ToSwTicks(_opt.HardLockoutSeconds);
            }

            return new LimitDecision
            {
                Allowed = false,
                RetryAfterMs = retryMs,
                Credit = GetCredit(state.MicroBalance, _opt.TokenScale),
                Reason = _opt.HardLockoutSeconds > 0 ? RateLimitReason.HardLockout : RateLimitReason.SoftThrottle
            };
        }
    }

    /// <summary>
    /// Checks and consumes 1 token for the given IP (IPv4/IPv6).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public LimitDecision Check(System.Net.IPAddress ip)
    {
        System.ArgumentNullException.ThrowIfNull(ip);
        return Check(NetAddressKey.FromIpAddress(ip));
    }

    /// <summary>
    /// Checks and consumes 1 token for the given remote endpoint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public LimitDecision Check(System.Net.IPEndPoint endpoint)
    {
        System.ArgumentNullException.ThrowIfNull(endpoint);
        return Check(endpoint.Address);
    }

    /// <summary>
    /// Checks and consumes 1 token for the given remote endpoint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public LimitDecision Check(System.Net.EndPoint endpoint)
    {
        System.ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint is not System.Net.IPEndPoint ipEndPoint)
        {
            throw new System.ArgumentException("Only IPEndPoint is supported.", nameof(endpoint));
        }

        return Check(ipEndPoint.Address);
    }

    #endregion Public API

    #region Private Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Shard GetShard(NetAddressKey key)
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
        var dt = now - state.LastRefillSwTicks;
        if (dt <= 0)
        {
            return;
        }

        // Add (dt / sec) * refillPerSecMicro, using integer math
        // microToAdd = (dt * refillPerSecMicro) / StopwatchFrequency
        var microToAdd = (state.MicroBalance < _capacityMicro)
            ? dt * _refillPerSecMicro / (System.Int64)_swFreq
            : 0;

        if (microToAdd > 0)
        {
            var nb = state.MicroBalance + microToAdd;
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
    private System.Int64 ToSwTicks(System.Int32 seconds)
        => (System.Int64)System.Math.Round(seconds * _swFreq);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 GetCredit(System.Int64 microBalance, System.Int32 tokenScale)
    {
        var t = microBalance / tokenScale;
        return t <= 0 ? (System.UInt16)0
             : t >= System.UInt16.MaxValue ? System.UInt16.MaxValue
             : (System.UInt16)t;
    }

    private static void ValidateOptions(TokenBucketOptions opt)
    {
        if (opt.CapacityTokens <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.CapacityTokens)} must be > 0");
        }

        if (opt.RefillTokensPerSecond <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.RefillTokensPerSecond)} must be > 0");
        }

        if (opt.TokenScale <= 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.TokenScale)} must be > 0");
        }

        // ShardCount must be power-of-two for bit-mask; adjust if needed.
        if ((opt.ShardCount & (opt.ShardCount - 1)) != 0)
        {
            throw new InternalErrorException($"{nameof(TokenBucketOptions.ShardCount)} must be a power of two (e.g., 64)");
        }
    }

    /// <summary>
    /// Generates a human-readable diagnostic report of the limiter state.
    /// Includes config overview, shard stats, and top endpoints by pressure.
    /// </summary>
    /// <returns>Formatted string report.</returns>
    public System.String GenerateReport()
    {
        // Snapshot all endpoints into a single list (allocation-bounded by map sizes).
        var snapshot = new System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<NetAddressKey, EndpointState>>(1024);

        System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();
        System.Int32 shardCount = _shards.Length;
        System.Int32 totalEndpoints = 0;
        System.Int32 hardBlockedCount = 0;

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

    #endregion Private Helpers

    #region Cleanup

    /// <summary>
    /// Periodic cleanup of stale endpoints to bound memory use.
    /// </summary>
    private void Cleanup()
    {
        if (_disposed)
        {
            return;
        }

        try
        {


            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var staleTicks = ToSwTicks(_opt.StaleEntrySeconds);

            System.Int32 removed = 0, visited = 0;

            foreach (var shard in _shards)
            {
                foreach (var kv in shard.Map)
                {
                    visited++;
                    var state = kv.Value;
                    if (now - state.LastSeenSw > staleTicks)
                    {
                        // Best-effort: try remove if still present
                        _ = shard.Map.TryRemove(kv.Key, out _);
                        removed++;
                    }
                }
            }

            if (removed > 0)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TokenBucketLimiter)}] Cleanup visited={visited} removed={removed}");
            }
        }
        catch (System.Exception ex) when (ex is not System.ObjectDisposedException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TokenBucketLimiter)}] cleanup-error msg={ex.Message}");
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
                                     .CancelRecurring(TaskNames.Recurring.WithKey(nameof(TokenBucketLimiter), this.GetHashCode())));

        await System.Threading.Tasks.Task.Yield();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TokenBucketLimiter)}] disposed");
    }

    #endregion IDisposable & IAsyncDisposable
}
