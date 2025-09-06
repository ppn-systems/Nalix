// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Shared.Configuration;
using Nalix.Shared.Injection;

namespace Nalix.Network.Throttling;

/// <summary>
/// High-performance token-bucket based rate limiter with per-endpoint state,
/// using Stopwatch ticks for time arithmetic and fixed-point token precision.
/// Provides precise Retry-After and Credit for client backoff and flow control.
/// </summary>
public sealed class RequestLimiter : System.IDisposable, System.IAsyncDisposable
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
        public readonly System.Collections.Concurrent.ConcurrentDictionary<System.String, EndpointState> Map
            = new(System.StringComparer.Ordinal);

        // No shard-wide lock necessary for map ops; per-key Gate handles mutation.
    }

    #endregion Private Types

    #region Fields

    private readonly TokenBucketOptions _opt;
    private readonly System.Int64 _capacityMicro;
    private readonly System.Int64 _refillPerSecMicro;
    private readonly System.Double _swFreq; // Stopwatch ticks per second

    private readonly Shard[] _shards;
    private readonly System.Threading.Timer _cleanupTimer;

    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Creates a new RequestLimiter with provided options.
    /// </summary>
    public RequestLimiter(TokenBucketOptions? options = null)
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

        _cleanupTimer = new System.Threading.Timer(static s =>
        {
            ((RequestLimiter)s!).Cleanup();
        }, this, System.TimeSpan.FromSeconds(_opt.CleanupIntervalSeconds), System.TimeSpan.FromSeconds(_opt.CleanupIntervalSeconds));

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(RequestLimiter)}] init cap={_opt.CapacityTokens} " +
                                       $"refill={_opt.RefillTokensPerSecond}/s scale={_opt.TokenScale} " +
                                       $"shards={_opt.ShardCount} stale_s={_opt.StaleEntrySeconds} " +
                                       $"cleanup_s={_opt.CleanupIntervalSeconds} hardlock_s={_opt.HardLockoutSeconds}");

    }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Checks and consumes 1 token for the given endpoint. Returns decision with RetryAfter and Credit.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public LimitDecision Check(System.String endPoint)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(RequestLimiter));

        if (System.String.IsNullOrWhiteSpace(endPoint))
        {
            throw new InternalErrorException($"[{nameof(RequestLimiter)}] EndPoint cannot be null or whitespace", nameof(endPoint));
        }

        System.Boolean isNew = false;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var shard = GetShard(endPoint);

        var state = shard.Map.GetOrAdd(endPoint, _ =>
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
                                    .Debug($"[{nameof(RequestLimiter)}] new-endpoint ep={EpKey(endPoint)}");
        }

        lock (state.Gate)
        {
            state.LastSeenSw = now;

            // Hard lockout?
            if (state.HardBlockedUntilSw > now)
            {
                var retryMsHard = ComputeMs(now, state.HardBlockedUntilSw);
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(RequestLimiter)}] hard-blocked ep={EpKey(endPoint)} retry_ms={retryMsHard}");

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
                                            .Trace($"[{nameof(RequestLimiter)}] allow ep={EpKey(endPoint)} credit={credit}");
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
    /// Async facade for <see cref="Check(System.String)"/> (no extra allocations).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask<LimitDecision> CheckAsync(System.String endPoint) => new(this.Check(endPoint));

    #endregion Public API

    #region Private Helpers

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Shard GetShard(System.String key)
    {
        // Deterministic hashing; simple but fast. You can replace with XxHash if needed.
        unchecked
        {
            System.Int32 h = 23;
            foreach (var c in key)
            {
                h = (h * 31) + c;
            }

            if (h < 0)
            {
                h = ~h + 1;
            }

            return _shards[h & (_shards.Length - 1)];
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int64 ToSwTicks(System.Int32 seconds)
        => (System.Int64)System.Math.Round(seconds * _swFreq);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String EpKey(System.String s)
    {
        // FNV-1a 32-bit, stable
        unchecked
        {
            System.UInt32 h = 2166136261;
            for (System.Int32 i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619;
            }
            return h.ToString("X8");
        }
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
                                        .Debug($"[{nameof(RequestLimiter)}] Cleanup visited={visited} removed={removed}");
            }
        }
        catch (System.Exception ex) when (ex is not System.ObjectDisposedException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(RequestLimiter)}] cleanup-error msg={ex.Message}");
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
        _cleanupTimer.Dispose();
        await System.Threading.Tasks.Task.Yield();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(RequestLimiter)}] disposed");
    }

    #endregion IDisposable & IAsyncDisposable
}
