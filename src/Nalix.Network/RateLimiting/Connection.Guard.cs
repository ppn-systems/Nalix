// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;
using Nalix.Network.Internal.Security;
using Nalix.Network.Internal.Transport;
using Nalix.Network.Options;

namespace Nalix.Network.RateLimiting;

/// <summary>
/// High-performance per-endpoint concurrent connection limiter.
/// Uses a hybrid approach: a sealed class entry (<see cref="ConnectionLimitEntry"/>) holds
/// a mutable <see cref="ConnectionLimitInfo"/> struct protected by a per-entry lock,
/// plus a <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> for rate-window tracking.
/// Supports automatic cleanup of stale entries to bound memory usage.
/// </summary>
[SkipLocalsInit]
[DebuggerNonUserCode]
public sealed partial class ConnectionGuard : IDisposable, IAsyncDisposable, IReportable, IWithLogging<ConnectionGuard>
{
    #region Constants

    private const int MinReportCapacity = 128;
    private const int MaxReportCapacity = 4096;

    #endregion Constants

    #region Fields

    private readonly ConnectionQuotaOptions _config;
    private readonly TrustedProxyOptions _proxyConfig;
    private readonly ConnectionGuardOptions _protectionConfig;

    private readonly long _windowTicks;
    private readonly int _maxPerEndpoint;
    private readonly int _maxGlobalConnections;
    private readonly long _logSuppressWindowTicks;

    private readonly TimeSpan _cleanupInterval;
    private readonly TimeSpan _inactivityThreshold;
    private readonly NetworkAccessList _accessList;
    private readonly NetworkBanRepository _banRepository;
    private readonly ConcurrentDictionary<SocketEndpoint, ConnectionLimitEntry> _map;

    private ILogger? _logger;

    private int _disposed;
    private int _reloadPending;
    private IRecurringHandle? _saveJob;
    private IRecurringHandle? _cleanupJob;
    private IRecurringHandle? _hotReloadJob;
    private System.IO.FileSystemWatcher? _configWatcher;

    /// <summary>
    /// Metrics for monitoring
    /// </summary>
    /// <summary>
    /// Metrics for monitoring
    /// </summary>
    private int _globalConnections;
    private long _totalConnectionAttempts;
    private long _totalRejections;
    private long _totalCleanedEntries;

    private double _ewmaConnectionRate;
    private long _ewmaLastUpdateTicks;
    private const double EwmaAlpha = 0.3;

    private readonly long _banCountDecayWindowTicks;

    #endregion Fields

    #region Properties

    /// <summary>Gets the recurring name used for cleanup operations.</summary>
    public static readonly string RecurringName;

    #endregion Properties

    #region Constructors

    static ConnectionGuard() => RecurringName = "conn.limit";

    /// <summary>
    /// Initializes a new <see cref="ConnectionGuard"/> with optional configuration.
    /// </summary>
    /// <param name="config">Configuration options. If null, uses global configuration.</param>
    /// <exception cref="InternalErrorException">Thrown when configuration validation fails.</exception>
    public ConnectionGuard(ConnectionQuotaOptions? config = null)
    {
        _proxyConfig = ConfigurationManager.Instance.Get<TrustedProxyOptions>();
        _protectionConfig = ConfigurationManager.Instance.Get<ConnectionGuardOptions>();
        _config = config ?? ConfigurationManager.Instance.Get<ConnectionQuotaOptions>();

        _config.Validate();
        _proxyConfig.Validate();
        _protectionConfig.Validate();

        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        _cleanupInterval = _config.CleanupInterval;
        _windowTicks = _config.ConnectionRateWindow.Ticks;
        _inactivityThreshold = _config.InactivityThreshold;
        _maxPerEndpoint = _config.MaxConnectionsPerIpAddress;
        _maxGlobalConnections = _protectionConfig.MaxConnections;
        _logSuppressWindowTicks = _protectionConfig.DDoSLogSuppressWindow.Ticks;

        _banRepository = new NetworkBanRepository(_logger);
        _accessList = new NetworkAccessList(_logger, _proxyConfig);
        _map = new System.Collections.Concurrent.ConcurrentDictionary<SocketEndpoint, ConnectionLimitEntry>();

        _banCountDecayWindowTicks = ConfigurationManager.Instance.Get<ConnectionBanStoreOptions>().BanCountDecayWindow.Ticks;

        _banRepository.Load(_map);

        this.INITIALIZE_METRICS();
        this.SCHEDULE_CLEANUP_JOB();
        this.INITIALIZE_HOT_RELOAD();

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("[NW.ConnectionGuard] init maxPerEndpoint={Limit} inactivity={InactivityThresholdTotalSeconds}s cleanup={CleanupIntervalTotalSeconds}s", _maxPerEndpoint, _inactivityThreshold.TotalSeconds, _cleanupInterval.TotalSeconds);
        }
    }


    /// <summary>Initializes a new <see cref="ConnectionGuard"/> using global configuration.</summary>
    public ConnectionGuard() : this(config: null) { }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Assigns a logger instance used by the limiter for diagnostic output.
    /// </summary>
    /// <param name="logger">The logger to use for subsequent diagnostics.</param>
    /// <returns>The current <see cref="ConnectionGuard"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ConnectionGuard WithLogging(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Manually bans an IP address for a specified duration.
    /// This bypasses progressive limits and applies the ban immediately.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BanEndpoint(IPAddress address, TimeSpan duration)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, nameof(ConnectionGuard));
        ArgumentNullException.ThrowIfNull(address);

        if (_accessList.IsTrustedProxy(address))
        {
            return;
        }

        SocketEndpoint key = SocketEndpoint.FromIpAddress(address);
        long nowTicks = Clock.NowUtc().Ticks;
        long banUntilTicks = nowTicks + duration.Ticks;

        // Get the entry or create a new one if the IP address has never connected.
        ConnectionLimitEntry entry = _map.GetOrAdd(key, static _ => new ConnectionLimitEntry());

        // Update ban duration under lock to ensure BannedUntilTicks and LastBanTimeTicks are written atomically
        bool lockTaken = false;
        try
        {
            entry.SpinLock.Enter(ref lockTaken);
            entry.BannedUntilTicks = banUntilTicks;
            entry.LastBanTimeTicks = nowTicks;
        }
        finally
        {
            if (lockTaken)
            {
                entry.SpinLock.Exit();
            }
        }

        // Mark as dirty so NetworkBanRepository saves it to a file in the next cycle
        _banRepository.MarkDirty();

        if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
        {
            DateTime banUntil = new(banUntilTicks, DateTimeKind.Utc);
            string banTime = banUntil.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            _logger.LogWarning("[NW.ConnectionGuard] manually-banned ip={Address} until={BanUntil}", address, banTime);
        }
    }

    /// <summary>
    /// Attempts to acquire a connection slot for the given endpoint.
    /// </summary>
    /// <param name="endPoint">The IP endpoint requesting connection.</param>
    /// <returns>True if connection is allowed; false if limit exceeded.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if limiter is disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown if IPEndPoint is null.</exception>"
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryAccept(IPEndPoint endPoint)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, nameof(ConnectionGuard));
        ArgumentNullException.ThrowIfNull(endPoint);

        _ = Interlocked.Increment(ref _totalConnectionAttempts);

        DateTime now = Clock.NowUtc();

        // 1. Invalid endpoint -> Reject
        if (endPoint.Address is null || endPoint.Address.Equals(IPAddress.Any) || endPoint.Address.Equals(IPAddress.IPv6Any))
        {
            _ = Interlocked.Increment(ref _totalRejections);
            return false;
        }

        // 2. Blacklist -> Reject
        if (_accessList.IsBlacklisted(endPoint.Address))
        {
            _ = Interlocked.Increment(ref _totalRejections);
            return false;
        }

        if (_maxGlobalConnections > -1)
        {
            while (true)
            {
                int current = Volatile.Read(ref _globalConnections);
                if (current >= _maxGlobalConnections)
                {
                    _ = Interlocked.Increment(ref _totalRejections);
                    return false;
                }
                if (Interlocked.CompareExchange(ref _globalConnections, current + 1, current) == current)
                {
                    break;
                }
            }
        }

        SocketEndpoint key = CONVERT_TO_NETWORK_ENDPOINT(endPoint);

        long attempts = Interlocked.Read(ref _totalConnectionAttempts);
        if (attempts % 100 == 0)
        {
            this.UPDATE_EWMA(now.Ticks);
        }

        ConnectionAllowResult result = this.TRY_ACQUIRE_CONNECTION_SLOT(key, now, endPoint.Address);

        if (result.Allowed)
        {
            SubnetAllowResult subnetResult = this.TRY_ACQUIRE_SUBNET_SLOT(endPoint.Address, now.Ticks);
            if (!subnetResult.Allowed)
            {
                _ = this.TRY_RELEASE_CONNECTION_SLOT(key, now);
                if (_maxGlobalConnections > -1)
                {
                    _ = Interlocked.Decrement(ref _globalConnections);
                }
                _ = Interlocked.Increment(ref _totalRejections);
                return false;
            }
        }

        if (!result.Allowed)
        {
            if (_maxGlobalConnections > -1)
            {
                _ = Interlocked.Decrement(ref _globalConnections);
            }

            _ = Interlocked.Increment(ref _totalRejections);

            // Throttled reject log — chỉ log 1 lần mỗi suppress window per IP
            if (_map.TryGetValue(key, out ConnectionLimitEntry? entry) && entry is not null)
            {
                long nowTicks = now.Ticks;
                long windowTicks = _logSuppressWindowTicks;

                if (ThrottledLogGate.TryAcquire(
                        ref entry.LastRejectLogTicks,
                        ref entry.SuppressedRejectCount,
                        nowTicks, windowTicks,
                        out long suppressed))
                {
                    string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

                    if (_logger != null && _logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("[NW.ConnectionGuard] reject endpoint={Endpoint} current={Current} limit={Limit}{Suffix}", endPoint, result.CurrentConnections, _maxPerEndpoint, suffix);
                    }
                }
            }
        }
        else
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[NW.ConnectionGuard] allow endpoint={Endpoint} current={Current} limit={Limit}", endPoint, result.CurrentConnections, _maxPerEndpoint);
            }
        }

        return result.Allowed;
    }

    /// <summary>
    /// Handles connection closure event and decrements the connection counter.
    /// </summary>
    /// <param name="sender">Event sender (unused).</param>
    /// <param name="args">Connection event arguments.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Because it's required by the event signature")]
    public void OnConnectionClosed(object? sender, IConnectEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (args?.Connection?.NetworkEndpoint is null)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("[NW.ConnectionGuard:Internal] received-null args/connection/endpoint");
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(args.Connection.NetworkEndpoint.Address))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("[NW.ConnectionGuard:Internal] received-empty-address");
            }
            return;
        }

        if (_maxGlobalConnections > -1)
        {
            _ = Interlocked.Decrement(ref _globalConnections);
        }

        DateTime now = Clock.NowUtc();
        SocketEndpoint key = SocketEndpoint.FromNetworkEndpoint(args.Connection.NetworkEndpoint);
        bool released = this.TRY_RELEASE_CONNECTION_SLOT(key, now);

        if (IPAddress.TryParse(key.Address, out System.Net.IPAddress? address1))
        {
            this.TRY_RELEASE_SUBNET_SLOT(address1, now);
        }

        if (released && _map.TryGetValue(key, out ConnectionLimitEntry? closedEntry) && closedEntry is not null)
        {
            long nowTicks = now.Ticks;

            if (args.Connection.UpTime < _config.ShortLivedThresholdMs && _config.ShortLivedThresholdMs > 0)
            {
                bool slLockTaken = false;
                try
                {
                    closedEntry.SpinLock.Enter(ref slLockTaken);
                    closedEntry.RecentConnectionTimestamps.Enqueue(nowTicks);
                }
                finally
                {
                    if (slLockTaken)
                    {
                        closedEntry.SpinLock.Exit();
                    }
                }
            }

            long windowTicks = _logSuppressWindowTicks;

            if (ThrottledLogGate.TryAcquire(
                    ref closedEntry.LastClosedLogTicks,
                    ref closedEntry.SuppressedClosedCount,
                    nowTicks, windowTicks,
                    out long suppressed))
            {
                string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

                if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[NW.ConnectionGuard] closed endpoint={Address}{Suffix}", key.Address, suffix);
                }
            }
        }
    }

    /// <summary>
    /// Checks if the provided endpoint belongs to a known trusted proxy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTrustedProxy(IPEndPoint? endPoint) => endPoint?.Address != null && _accessList.IsTrustedProxy(endPoint.Address);

    /// <summary>
    /// Safely decrements the connection counter without requiring an IConnection.
    /// Used for rollback when connection initialization fails after TryAccept succeeded.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Release(IPEndPoint endPoint)
    {
        if (Volatile.Read(ref _disposed) != 0 || endPoint?.Address is null)
        {
            return;
        }

        if (_maxGlobalConnections > -1)
        {
            _ = Interlocked.Decrement(ref _globalConnections);
        }

        DateTime now = Clock.NowUtc();
        SocketEndpoint key = CONVERT_TO_NETWORK_ENDPOINT(endPoint);
        _ = this.TRY_RELEASE_CONNECTION_SLOT(key, now);
        if (IPAddress.TryParse(key.Address, out System.Net.IPAddress? address2))
        {
            this.TRY_RELEASE_SUBNET_SLOT(address2, now);
        }
    }

    #endregion Public API

    #region Connection Slot Management

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UPDATE_EWMA(long nowTicks)
    {
        double elapsed = (nowTicks - _ewmaLastUpdateTicks) / (double)TimeSpan.TicksPerSecond;
        if (elapsed > 0)
        {
            double currentRate = Interlocked.Read(ref _totalConnectionAttempts) / elapsed;
            _ewmaConnectionRate = (EwmaAlpha * currentRate) + ((1 - EwmaAlpha) * _ewmaConnectionRate);
            _ewmaLastUpdateTicks = nowTicks;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GET_EFFECTIVE_MAX_PER_ENDPOINT(bool isTrustedProxy)
    {
        int baseMax = isTrustedProxy ? _proxyConfig.MaxConnectionsPerTrustedProxy : _maxPerEndpoint;

        if (!_config.EnableAdaptiveMode || _maxGlobalConnections <= -1)
        {
            return baseMax;
        }

        double loadRatio = (double)Volatile.Read(ref _globalConnections) / _maxGlobalConnections;
        if (loadRatio > _config.AdaptiveLoadThreshold)
        {
            return Math.Max(1, (int)(baseMax * _config.AdaptiveTighteningFactor));
        }

        return baseMax;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SocketEndpoint CONVERT_TO_NETWORK_ENDPOINT(IPEndPoint endPoint) => SocketEndpoint.FromIpAddress(endPoint.Address);

    /// <summary>
    /// Attempts to acquire a connection slot.
    /// Uses GetOrAdd to safely retrieve-or-create the entry, then locks the entry
    /// for the counter mutation. The rate-window queue is trimmed before the check.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="now"></param>
    /// <param name="address"></param>
    private ConnectionAllowResult TRY_ACQUIRE_CONNECTION_SLOT(SocketEndpoint key, DateTime now, IPAddress address)
    {
        // 3. Trusted proxy check
        bool isTrustedProxy = _accessList.IsTrustedProxy(address);

        int maxConnections = this.GET_EFFECTIVE_MAX_PER_ENDPOINT(isTrustedProxy);
        int maxAttempts = isTrustedProxy ? _proxyConfig.MaxAttemptsPerTrustedProxyWindow : _config.MaxConnectionsPerWindow;
        long nowTicks = now.Ticks;

        while (true)
        {
            if (!_map.TryGetValue(key, out ConnectionLimitEntry? entry) || entry is null)
            {
                entry = _map.GetOrAdd(key, static _ => new ConnectionLimitEntry());
            }

            // Update LastSeen (Lock-free since it's just a long write and we don't strictly need exact consistency for cleanup/debug)
            _ = Interlocked.Exchange(ref entry.LastSeenAtTicks, nowTicks);

            long bannedUntil = Interlocked.Read(ref entry.BannedUntilTicks);

            bool trimLockTaken = false;
            try
            {
                entry.SpinLock.Enter(ref trimLockTaken);
                this.TRIM_OLD_TIMESTAMPS(entry.RecentConnectionTimestamps, nowTicks);

                long decayWindowTicks = _banCountDecayWindowTicks;
                if (entry.BanCount > 0 && entry.LastBanTimeTicks > 0)
                {
                    long elapsed = nowTicks - entry.LastBanTimeTicks;
                    if (elapsed > decayWindowTicks)
                    {
                        int decayTiers = (int)(elapsed / decayWindowTicks);
                        entry.BanCount = Math.Max(0, entry.BanCount - decayTiers);
                        if (entry.BanCount == 0)
                        {
                            entry.LastBanTimeTicks = 0;
                        }
                    }
                }
            }
            finally
            {
                if (trimLockTaken)
                {
                    entry.SpinLock.Exit();
                }
            }

            // 4. Runtime ban active -> Reject (Trusted proxies are never banned at runtime)
            if (!isTrustedProxy && bannedUntil > nowTicks)
            {
                ThrottledLogGate.LogBanned(_logger, entry, key.Address, nowTicks, _logSuppressWindowTicks, new DateTime(bannedUntil, DateTimeKind.Utc));

                int currentConns;
                bool lockTaken = false;
                try
                {
                    entry.SpinLock.Enter(ref lockTaken);
                    currentConns = entry.Info.CurrentConnections;
                }
                finally
                {
                    if (lockTaken)
                    {
                        entry.SpinLock.Exit();
                    }
                }

                return new ConnectionAllowResult { Allowed = false, CurrentConnections = currentConns };
            }

            // Declare the lock beforehand to use after exiting the lock.
            ConnectionAllowResult result;

            bool spinLockTaken = false;
            try
            {
                entry.SpinLock.Enter(ref spinLockTaken);

                if (entry.IsRemoved)
                {
                    continue; // Retry with a fresh GetOrAdd, this one is tombstoned
                }

                long lastAccept = entry.LastAcceptTimeTicks;
                long gap = (nowTicks - lastAccept) / TimeSpan.TicksPerMillisecond;
                bool isBurst = lastAccept > 0
                    && gap < _config.MinConnectionIntervalMs
                    && entry.RecentConnectionTimestamps.Count >= _config.BurstThreshold;

                int effectiveMaxAttempts = isBurst
                    ? Math.Max(1, maxAttempts / _config.BurstPenaltyDivisor)
                    : maxAttempts;

                if (entry.RecentConnectionTimestamps.Count >= effectiveMaxAttempts)
                {
                    // 6. On violation -> Update ban state (if not trusted)
                    if (!isTrustedProxy)
                    {
                        entry.BanCount++;
                        entry.LastBanTimeTicks = nowTicks;

                        TimeSpan banDuration = this.CALCULATE_PROGRESSIVE_BAN_DURATION(entry.BanCount);
                        long banUntilTicks = nowTicks + banDuration.Ticks;
                        _ = Interlocked.Exchange(ref entry.BannedUntilTicks, banUntilTicks);
                        _banRepository.MarkDirty();

                        ThrottledLogGate.LogDDoSDetected(_logger, entry, key.Address, nowTicks, _logSuppressWindowTicks);

                        if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                        {
                            DateTime banUntil = new(banUntilTicks, DateTimeKind.Utc);
                            string banTime = banUntil.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                            _logger.LogWarning("[NW.ConnectionGuard] banned ip={Address} count={BanCount} until={BanUntil}", key.Address, entry.BanCount, banTime);
                        }
                    }

                    result = new ConnectionAllowResult
                    {
                        Allowed = false,
                        CurrentConnections = entry.Info.CurrentConnections
                    };
                }
                else if (entry.Info.CurrentConnections >= maxConnections)
                {
                    // Concurrent connection limit reached for this IP
                    result = new ConnectionAllowResult
                    {
                        Allowed = false,
                        CurrentConnections = entry.Info.CurrentConnections
                    };
                }
                else
                {
                    int newTotalToday = CALCULATE_TOTAL_CONNECTIONS_TODAY(entry.Info, now, _config.DailyResetTimeOffset);

                    entry.Info = entry.Info with
                    {
                        CurrentConnections = entry.Info.CurrentConnections + 1,
                        TotalConnectionsToday = newTotalToday,
                        LastConnectionTime = now
                    };

                    entry.RecentConnectionTimestamps.Enqueue(nowTicks);
                    entry.LastAcceptTimeTicks = nowTicks;

                    result = new ConnectionAllowResult { Allowed = true, CurrentConnections = entry.Info.CurrentConnections };
                }
            }
            finally
            {
                if (spinLockTaken)
                {
                    entry.SpinLock.Exit();
                }
            }

            return result;
        }
    }

    /// <summary>Removes timestamps outside the rate-window.</summary>
    /// <param name="timestamps"></param>
    /// <param name="nowTicks"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TRIM_OLD_TIMESTAMPS(System.Collections.Generic.Queue<long> timestamps, long nowTicks)
    {
        long cutoff = nowTicks - _windowTicks;

        while (timestamps.TryPeek(out long oldest) && oldest < cutoff)
        {
            _ = timestamps.Dequeue();
        }

        int hardCap = _config.MaxConnectionsPerWindow * 4;
        while (timestamps.Count > hardCap)
        {
            _ = timestamps.Dequeue();
        }
    }

    private TimeSpan CALCULATE_PROGRESSIVE_BAN_DURATION(int banCount)
    {
        if (!_protectionConfig.EnableProgressiveBanning)
        {
            return _protectionConfig.BanDuration;
        }

        // Progressive schedule: 1m, 5m, 15m, 1h, 6h, 24h
        return banCount switch
        {
            <= 1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            4 => TimeSpan.FromHours(1),
            5 => TimeSpan.FromHours(6),
            _ => TimeSpan.FromHours(24) // Cap at 24 hours
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CALCULATE_TOTAL_CONNECTIONS_TODAY(ConnectionLimitInfo info, DateTime now, TimeSpan offset)
    {
        if (info.LastConnectionTime == default)
        {
            return 1;
        }

        // Apply offset to both times to determine the "logical day" for reset
        DateTime logicalToday = (now + offset).Date;
        DateTime logicalLastConnection = (info.LastConnectionTime + offset).Date;

        // Use strict inequality (!=) to handle backward NTP syncs and forward day changes.
        // NOTE: When NTP adjusts clock backward within the same calendar day,
        // the counter does NOT reset (logicalLastConnection == logicalToday).
        // This is by design: resetting on small NTP drifts would allow counter manipulation.
        // The counter only resets on actual day boundaries.
        if (logicalLastConnection != logicalToday)
        {
            return 1; // Different day, reset counter
        }

        // Prevent overflow
        return info.TotalConnectionsToday >= int.MaxValue - 1 ? int.MaxValue : info.TotalConnectionsToday + 1;
    }

    /// <summary>
    /// Releases a connection slot for the given endpoint.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="now"></param>
    private bool TRY_RELEASE_CONNECTION_SLOT(SocketEndpoint key, DateTime now)
    {
        if (!_map.TryGetValue(key, out ConnectionLimitEntry? entry) || entry is null)
        {
            return false;
        }

        bool lockTaken = false;
        try
        {
            entry.SpinLock.Enter(ref lockTaken);
            if (entry.IsRemoved)
            {
                return false;
            }

            // Decrement with underflow protection
            int newCount = Math.Max(0, entry.Info.CurrentConnections - 1);

            entry.Info = entry.Info with
            {
                CurrentConnections = newCount,
                LastConnectionTime = now
            };

            // Clear queue if no connections and queue is large
            if (newCount == 0 && entry.RecentConnectionTimestamps.Count > _config.MaxConnectionsPerWindow * 2)
            {
                entry.RecentConnectionTimestamps.Clear();

                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("[NW.ConnectionGuard] cleared-queue ip={Address} reason=oversized", key.Address);
                }
            }

            this.TRIM_OLD_TIMESTAMPS(entry.RecentConnectionTimestamps, now.Ticks);
        }
        finally
        {
            if (lockTaken)
            {
                entry.SpinLock.Exit();
            }
        }

        return true;
    }

    #endregion Connection Slot Management

    #region IDisposable & IAsyncDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _cleanupJob?.Dispose();
            _saveJob?.Dispose();
            _hotReloadJob?.Dispose();

            if (_configWatcher != null)
            {
                _configWatcher.EnableRaisingEvents = false;
                _configWatcher.Dispose();
            }

            if (_banRepository.IsEnabled)
            {
                _banRepository.Save(_map); // Save snapshot BEFORE clearing the map
            }

            _map.Clear();

            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("[NW.ConnectionGuard:Dispose] disposed");
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "[NW.ConnectionGuard:Dispose] dispose-error");
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        this.Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion IDisposable & IAsyncDisposable
}
