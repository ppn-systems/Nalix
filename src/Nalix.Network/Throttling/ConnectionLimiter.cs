// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Shared.Configuration;
using Nalix.Shared.Injection;

namespace Nalix.Network.Throttling;

/// <summary>
/// High-performance per-IP concurrent connection limiter.
/// Uses lock-free CAS updates on a ConcurrentDictionary to avoid lost updates under contention.
/// Supports cleanup of stale entries to bound memory usage.
/// </summary>
public sealed class ConnectionLimiter : System.IDisposable
{
    #region Constants

    private const System.Int32 MaxCleanupKeysPerRun = 1000;
    private static readonly System.DateTime UnixEpochUtc = new(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

    #endregion Constants

    #region Fields

    private readonly ConnLimitOptions _config;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Net.IPAddress, ConnectionLimitInfo> _map;
    private readonly System.Threading.Timer _cleanupTimer;
    private readonly System.Threading.SemaphoreSlim _cleanupGate = new(1, 1);

    private readonly System.Int32 _maxPerIp;
    private readonly System.TimeSpan _inactivityThreshold;
    private readonly System.TimeSpan _cleanupInterval;

    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="ConnectionLimiter"/> using configuration from <see cref="ConfigurationManager"/>.
    /// </summary>
    public ConnectionLimiter()
        : this((ConnLimitOptions?)null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="ConnectionLimiter"/> with an optional config. If <paramref name="config"/> is null, global config is used.
    /// </summary>
    public ConnectionLimiter(ConnLimitOptions? config)
    {
        _config = config ?? ConfigurationManager.Instance.Get<ConnLimitOptions>();
        Validate(_config);

        _maxPerIp = _config.MaxConnectionsPerIpAddress;
        _inactivityThreshold = _config.InactivityThreshold;
        _cleanupInterval = _config.CleanupInterval;

        _map = new System.Collections.Concurrent.ConcurrentDictionary<System.Net.IPAddress, ConnectionLimitInfo>();

        _cleanupTimer = new System.Threading.Timer(static s =>
        {
            ((ConnectionLimiter)s!).RunCleanupTick();
        }, this, _cleanupInterval, _cleanupInterval);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
            .Debug($"[{nameof(ConnectionLimiter)}] init: maxPerIp={_maxPerIp}, inactivity={_inactivityThreshold.TotalSeconds:F0}s, cleanup={_cleanupInterval.TotalSeconds:F0}s");
    }

    /// <summary>
    /// Initializes a new <see cref="ConnectionLimiter"/> and allows caller to mutate the config prior to use.
    /// </summary>
    public ConnectionLimiter(System.Action<ConnLimitOptions>? configure)
        : this(CreateConfigured(configure))
    {
    }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Attempts to acquire a connection slot for the given IP address.
    /// Returns <c>true</c> if allowed (and increments counters), otherwise <c>false</c>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsConnectionAllowed([System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress endPoint)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (endPoint is null)
        {
            throw new InternalErrorException($"[{nameof(ConnectionLimiter)}] EndPoint cannot be null", nameof(endPoint));
        }

        // Normalize IPv4-mapped IPv6 as IPv4 to avoid duplicate keys for the same client.
        if (endPoint.IsIPv4MappedToIPv6)
        {
            endPoint = endPoint.MapToIPv4();
        }

        var nowUtc = System.DateTime.UtcNow;
        var todayUtc = nowUtc.Date;

        // Ensure an entry exists
        _ = _map.TryAdd(endPoint, new ConnectionLimitInfo(
            currentConnections: 0,
            lastConnectionTime: nowUtc,
            totalConnectionsToday: 0));

        // CAS loop: read → compute → TryUpdate until success
        while (true)
        {
            if (!_map.TryGetValue(endPoint, out var existing))
            {
                continue; // transient; try again
            }

            if (existing.CurrentConnections >= _maxPerIp)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Trace($"[{nameof(ConnectionLimiter)}] over limit {endPoint}: {existing.CurrentConnections}/{_maxPerIp}");
                return false;
            }

            var totalToday = (todayUtc > existing.LastConnectionTime.Date)
                ? 1
                : existing.TotalConnectionsToday + 1;

            var proposed = existing with
            {
                CurrentConnections = existing.CurrentConnections + 1,
                LastConnectionTime = nowUtc,
                TotalConnectionsToday = totalToday
            };

            if (_map.TryUpdate(endPoint, proposed, existing))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Trace($"[{nameof(ConnectionLimiter)}] allow {endPoint}: now={proposed.CurrentConnections}/{_maxPerIp}");
                return true;
            }
            // else: raced, retry
        }
    }

    /// <summary>
    /// Marks a connection as closed for the given IP address. Returns <c>true</c> if the counter was decremented.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean ConnectionClosed([System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress endPoint)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (endPoint is null)
        {
            throw new InternalErrorException($"[{nameof(ConnectionLimiter)}] EndPoint cannot be null", nameof(endPoint));
        }

        if (endPoint.IsIPv4MappedToIPv6)
        {
            endPoint = endPoint.MapToIPv4();
        }

        var nowUtc = System.DateTime.UtcNow;

        if (!_map.TryGetValue(endPoint, out _))
        {
            return false;
        }

        while (true)
        {
            if (!_map.TryGetValue(endPoint, out var existing))
            {
                return false;
            }

            var proposed = existing with
            {
                CurrentConnections = System.Math.Max(0, existing.CurrentConnections - 1),
                LastConnectionTime = nowUtc
            };

            if (_map.TryUpdate(endPoint, proposed, existing))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Trace($"[{nameof(ConnectionLimiter)}] close {endPoint}: now={proposed.CurrentConnections}/{_maxPerIp}");
                return true;
            }
        }
    }

    /// <summary>
    /// Gets per-IP counters. If IP is unknown, returns zeros and <see cref="UnixEpochUtc"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (System.Int32 Current, System.Int32 TotalToday, System.DateTime LastUtc) GetConnectionInfo(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress endPoint)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (endPoint is null)
        {
            throw new InternalErrorException($"[{nameof(ConnectionLimiter)}] EndPoint cannot be null", nameof(endPoint));
        }

        if (endPoint.IsIPv4MappedToIPv6)
        {
            endPoint = endPoint.MapToIPv4();
        }

        if (_map.TryGetValue(endPoint, out var info))
        {
            return (info.CurrentConnections, info.TotalConnectionsToday, info.LastConnectionTime);
        }
        return (0, 0, UnixEpochUtc);
    }

    /// <summary>
    /// Returns a snapshot of all tracked IPs and their (Current, TotalToday) counters.
    /// </summary>
    public System.Collections.Generic.Dictionary<System.Net.IPAddress, (System.Int32 Current, System.Int32 TotalToday)> GetAllConnections()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new System.Collections.Generic.Dictionary<System.Net.IPAddress, (System.Int32, System.Int32)>(_map.Count);
        foreach (var kv in _map)
        {
            result[kv.Key] = (kv.Value.CurrentConnections, kv.Value.TotalConnectionsToday);
        }
        return result;
    }

    /// <summary>
    /// Returns the total number of concurrently open connections across all IPs.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Int32 GetTotalConnectionCount()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        System.Int32 total = 0;
        foreach (var v in _map.Values)
        {
            total += v.CurrentConnections;
        }

        return total;
    }

    /// <summary>
    /// Resets all counters and clears the internal map.
    /// </summary>
    public void ResetAllCounters()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        _map.Clear();
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
            .Info($"[{nameof(ConnectionLimiter)}] reset all counters");
    }

    /// <summary>
    /// RAII-style helper: acquire on success and auto-decrement on dispose.
    /// </summary>
    public ConnectionLease TryAcquire([System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress endPoint, out System.Boolean allowed)
    {
        allowed = IsConnectionAllowed(endPoint);
        return new ConnectionLease(this, endPoint, allowed);
    }

    #endregion Public API

    #region Cleanup

    /// <summary>
    /// Timer tick entry: non-reentrant, best-effort cleanup of stale entries.
    /// </summary>
    private async void RunCleanupTick()
    {
        if (_disposed)
        {
            return;
        }

        // Try non-blocking; skip if a previous cleanup is still running.
        if (!await _cleanupGate.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var nowUtc = System.DateTime.UtcNow;
            var cutoff = nowUtc - _inactivityThreshold;

            System.Int32 scanned = 0, removed = 0;

            foreach (var kv in _map)
            {
                if (scanned++ >= MaxCleanupKeysPerRun)
                {
                    break;
                }

                var info = kv.Value;
                if (info.CurrentConnections <= 0 && info.LastConnectionTime < cutoff)
                {
                    _ = _map.TryRemove(kv.Key, out _);
                    removed++;
                }
            }

            if (removed > 0)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Debug($"[{nameof(ConnectionLimiter)}] cleanup: scanned={scanned}, removed={removed}");
            }
        }
        catch (System.Exception ex) when (ex is not System.ObjectDisposedException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[{nameof(ConnectionLimiter)}] cleanup error: {ex.Message}");
        }
        finally
        {
            _ = _cleanupGate.Release();
        }
    }

    #endregion Cleanup

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _cleanupTimer.Dispose();
            _cleanupGate.Dispose();
            _map.Clear();
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[{nameof(ConnectionLimiter)}] dispose error: {ex.Message}");
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    #region Helpers

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Validate(ConnLimitOptions opt)
    {
        if (opt.MaxConnectionsPerIpAddress <= 0)
        {
            throw new InternalErrorException($"{nameof(ConnLimitOptions.MaxConnectionsPerIpAddress)} must be > 0");
        }

        if (opt.InactivityThreshold <= System.TimeSpan.Zero)
        {
            throw new InternalErrorException($"{nameof(ConnLimitOptions.InactivityThreshold)} must be > 0");
        }

        if (opt.CleanupInterval <= System.TimeSpan.Zero)
        {
            throw new InternalErrorException($"{nameof(ConnLimitOptions.CleanupInterval)} must be > 0");
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ConnLimitOptions CreateConfigured(System.Action<ConnLimitOptions>? configure)
    {
        var cfg = ConfigurationManager.Instance.Get<ConnLimitOptions>();
        configure?.Invoke(cfg);
        return cfg;
    }

    /// <summary>
    /// Disposable lease to auto-decrement <see cref="ConnectionClosed"/> when disposed.
    /// </summary>
    public readonly struct ConnectionLease(ConnectionLimiter owner, System.Net.IPAddress ip, System.Boolean valid) : System.IDisposable
    {
        private readonly System.Net.IPAddress _ip = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

        /// <inheritdoc />
        public void Dispose()
        {
            if (valid)
            {
                _ = owner.ConnectionClosed(_ip);
            }
        }
    }

    /// <summary>
    /// Stores connection tracking data for an IP address.
    /// Optimized as a readonly record struct for performance and memory usage.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay(
        "Current: {CurrentConnections}, Total: {TotalConnectionsToday}, Last: {LastConnectionTime}")]
    internal readonly record struct ConnectionLimitInfo
    {
        /// <summary>
        /// The current TransportProtocol of active connections.
        /// </summary>
        public readonly System.Int32 CurrentConnections { get; init; }

        /// <summary>
        /// When the most recent connection was established.
        /// </summary>
        public readonly System.DateTime LastConnectionTime { get; init; }

        /// <summary>
        /// The total TransportProtocol of connections made today.
        /// </summary>
        public readonly System.Int32 TotalConnectionsToday { get; init; }

        /// <summary>
        /// Creates a new connection info record.
        /// </summary>
        public ConnectionLimitInfo(
            System.Int32 currentConnections,
            System.DateTime lastConnectionTime,
            System.Int32 totalConnectionsToday)
        {
            this.CurrentConnections = currentConnections;
            this.LastConnectionTime = lastConnectionTime;
            this.TotalConnectionsToday = totalConnectionsToday;
        }
    }

    #endregion Helpers
}
