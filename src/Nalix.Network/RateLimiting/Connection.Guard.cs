// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Environment.IO;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Pools;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
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
public sealed class ConnectionGuard : IDisposable, IAsyncDisposable, IReportable, IWithLogging<ConnectionGuard>
{
    #region Constants

    private const int MinReportCapacity = 128;
    private const int MaxReportCapacity = 4096;

    #endregion Constants

    #region Fields

    private readonly ConnectionQuotaOptions _config;
    private readonly TrustedProxyOptions _proxyConfig;
    private readonly ConnectionBanStoreOptions _storeConfig;
    private readonly ConnectionGuardOptions _protectionConfig;

    private readonly long _windowTicks;
    private readonly int _maxPerEndpoint;
    private readonly long _logSuppressWindowTicks;

    private readonly TimeSpan _cleanupInterval;
    private readonly TimeSpan _inactivityThreshold;
    private readonly List<IPNetwork> _trustedProxies = new();
    private readonly HashSet<IPAddress> _blacklistedIps = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<SocketEndpoint, ConnectionLimitEntry> _map;

    private ILogger? _logger;

    private int _disposed;
    private int _persistenceDirty;

    private Action<INetworkEndpoint>? _onEndpointTerminationRequested;

    /// <summary>
    /// Metrics for monitoring
    /// </summary>
    private long _totalConnectionAttempts;
    private long _totalRejections;
    private long _totalCleanedEntries;

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
        _config = config ?? ConfigurationManager.Instance.Get<ConnectionQuotaOptions>();
        _config.Validate();

        _protectionConfig = ConfigurationManager.Instance.Get<ConnectionGuardOptions>();
        _protectionConfig.Validate();

        _proxyConfig = ConfigurationManager.Instance.Get<TrustedProxyOptions>();
        _proxyConfig.Validate();

        _storeConfig = ConfigurationManager.Instance.Get<ConnectionBanStoreOptions>();
        _storeConfig.Validate();

        this.PARSE_IP_CONFIG();

        _maxPerEndpoint = _config.MaxConnectionsPerIpAddress;
        _cleanupInterval = _config.CleanupInterval;
        _inactivityThreshold = _config.InactivityThreshold;

        _windowTicks = _config.ConnectionRateWindow.Ticks;
        _logSuppressWindowTicks = _protectionConfig.DDoSLogSuppressWindow.Ticks;

        _map = new System.Collections.Concurrent.ConcurrentDictionary<SocketEndpoint, ConnectionLimitEntry>();

        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        this.LOAD_BANNED_IPS();

        this.INITIALIZE_METRICS();
        this.SCHEDULE_CLEANUP_JOB();

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug($"[NW.{nameof(ConnectionGuard)}] init " +
                          $"maxPerEndpoint={_maxPerEndpoint} " +
                          $"inactivity={_inactivityThreshold.TotalSeconds:F0}s " +
                          $"cleanup={_cleanupInterval.TotalSeconds:F0}s");
        }
    }

    private void PARSE_IP_CONFIG()
    {
        if (!string.IsNullOrWhiteSpace(_proxyConfig.TrustedProxiesString))
        {
            string[] parts = _proxyConfig.TrustedProxiesString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                if (IPNetwork.TryParse(part, out IPNetwork network))
                {
                    _trustedProxies.Add(network);
                }
                else if (IPAddress.TryParse(part, out IPAddress? ip))
                {
                    _trustedProxies.Add(new IPNetwork(ip, ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_protectionConfig.BlacklistedIpsString))
        {
            string[] parts = _protectionConfig.BlacklistedIpsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                if (IPAddress.TryParse(part, out IPAddress? ip))
                {
                    _ = _blacklistedIps.Add(ip);
                }
            }
        }
    }

    private void LOAD_BANNED_IPS()
    {
        if (!_storeConfig.Enabled)
        {
            return;
        }

        string path = System.IO.Path.Combine(Directories.DataDirectory, _storeConfig.StoreFileName);
        DateTime now = Clock.NowUtc();

        List<ConnectionBanRecord> records = ConnectionBanStore.Load(path, _storeConfig.MaxPersistedBans, _storeConfig.BanCountDecayWindow, now.Ticks);

        foreach (ConnectionBanRecord record in records)
        {
            ConnectionLimitEntry entry = new()
            {
                BannedUntilTicks = record.BannedUntilTicks,
                BanCount = record.BanCount,
                LastBanTimeTicks = record.LastBanTimeTicks,
                LastSeenAtTicks = record.LastSeenAtTicks
            };

            _ = _map.TryAdd(SocketEndpoint.FromNetworkEndpoint(record.Endpoint), entry);
        }

        if (records.Count > 0 && _logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation($"[NW.{nameof(ConnectionGuard)}] Loaded {records.Count} persisted bans.");
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
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an endpoint should be terminated.
    /// </summary>
    /// <param name="action">The action to invoke with the network endpoint.</param>
    /// <returns>The current <see cref="ConnectionGuard"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ConnectionGuard WithEndpointTermination(Action<INetworkEndpoint> action)
    {
        _onEndpointTerminationRequested += action ?? throw new ArgumentNullException(nameof(action));
        return this;
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

        // 2. Blacklist -> Reject (O(1) lookup)
        if (_blacklistedIps.Contains(endPoint.Address))
        {
            _ = Interlocked.Increment(ref _totalRejections);
            return false;
        }

        SocketEndpoint key = CONVERT_TO_NETWORK_ENDPOINT(endPoint);
        ConnectionAllowResult result = this.TRY_ACQUIRE_CONNECTION_SLOT(key, now, endPoint.Address);

        if (!result.Allowed)
        {
            _ = Interlocked.Increment(ref _totalRejections);

            // Throttled reject log — chỉ log 1 lần mỗi suppress window per IP
            if (_map.TryGetValue(key, out ConnectionLimitEntry? entry) && entry is not null)
            {
                long nowTicks = now.Ticks;
                long windowTicks = _logSuppressWindowTicks;

                if (TRY_ACQUIRE_LOG_SLOT(
                        ref entry.LastRejectLogTicks,
                        ref entry.SuppressedRejectCount,
                        nowTicks, windowTicks,
                        out long suppressed))
                {
                    string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

                    if (_logger != null && _logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation(
                            $"[NW.{nameof(ConnectionGuard)}] reject endpoint={endPoint} " +
                            $"current={result.CurrentConnections} limit={_maxPerEndpoint}{suffix}");
                    }
                }
            }
        }
        else
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"[NW.{nameof(ConnectionGuard)}] allow endpoint={endPoint} current={result.CurrentConnections} limit={_maxPerEndpoint}");
            }
        }

        return result.Allowed;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SCHEDULE_ENDPOINT_TERMINATION(SocketEndpoint key)
    {
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{TaskNaming.Tags.Worker}.{TaskNaming.Tags.Process}",
            group: $"{TaskNaming.Tags.Worker}",
            work: async (_, _) =>
            {
                try
                {
                    this.TRIGGER_ENDPOINT_TERMINATION_UPSTREAM(key);
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, $"[NW.{nameof(ConnectionGuard)}] endpoint-termination-failed ip={key.Address}");
                    }
                }

                await Task.CompletedTask.ConfigureAwait(false);
            },
            options: new WorkerOptions
            {
                Tag = TaskNaming.Tags.Net,
                RetainFor = TimeSpan.Zero,
                IdType = SnowflakeType.System,
            }
        );
    }

    /// <summary>
    /// Handles connection closure event and decrements the connection counter.
    /// </summary>
    /// <param name="sender">Event sender (unused).</param>
    /// <param name="args">Connection event arguments.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Event handler signature")]
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
                _logger.LogWarning($"[NW.{nameof(ConnectionGuard)}:Internal] received-null args/connection/endpoint");
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(args.Connection.NetworkEndpoint.Address))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning($"[NW.{nameof(ConnectionGuard)}:Internal] received-empty-address");
            }
            return;
        }

        DateTime now = Clock.NowUtc();
        SocketEndpoint key = SocketEndpoint.FromNetworkEndpoint(args.Connection.NetworkEndpoint);
        bool released = this.TRY_RELEASE_CONNECTION_SLOT(key, now);

        if (released && _map.TryGetValue(key, out ConnectionLimitEntry? closedEntry) && closedEntry is not null)
        {
            long nowTicks = now.Ticks;
            long windowTicks = _logSuppressWindowTicks;

            if (TRY_ACQUIRE_LOG_SLOT(
                    ref closedEntry.LastClosedLogTicks,
                    ref closedEntry.SuppressedClosedCount,
                    nowTicks, windowTicks,
                    out long suppressed))
            {
                string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

                if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"[NW.{nameof(ConnectionGuard)}] closed endpoint={key.Address}{suffix}");
                }
            }
        }
    }

    /// <summary>
    /// Generates a human-readable diagnostic report of connection limiter state.
    /// </summary>
    /// <returns>Formatted report string.</returns>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        List<
            KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot = this.COLLECT_SNAPSHOT();

        try
        {
            SORT_SNAPSHOT_BY_LOAD(snapshot);
            return this.BUILD_REPORT(snapshot);
        }
        finally
        {
            RETURN_SNAPSHOT_TO_POOL(snapshot);
        }
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot = this.COLLECT_SNAPSHOT();
        try
        {
            SORT_SNAPSHOT_BY_LOAD(snapshot);
            GlobalMetrics metrics = this.CALCULATE_GLOBAL_METRICS(snapshot);

            writer.WriteStartObject();
            writer.WriteString("UtcNow", Clock.NowUtc());
            writer.WriteNumber("MaxPerEndpoint", _maxPerEndpoint);
            writer.WriteNumber("CleanupIntervalSeconds", _cleanupInterval.TotalSeconds);
            writer.WriteNumber("InactivityThresholdSeconds", _inactivityThreshold.TotalSeconds);
            writer.WriteNumber("TrackedEndpoints", metrics.TotalEndpoints);
            writer.WriteNumber("TotalConcurrent", metrics.TotalConcurrent);
            writer.WriteNumber("TotalAttempts", metrics.TotalAttempts);
            writer.WriteNumber("TotalRejections", metrics.TotalRejections);
            writer.WriteNumber("TotalCleaned", metrics.TotalCleaned);
            writer.WriteNumber("RejectionRate", metrics.TotalAttempts > 0 ? (metrics.TotalRejections * 100.0 / metrics.TotalAttempts) : 0.0);

            writer.WriteStartArray("TopEndpoints");
            int count = 0;
            foreach (KeyValuePair<INetworkEndpoint, ConnectionLimitInfo> kvp in snapshot)
            {
                if (count++ >= 50)
                {
                    break;
                }

                ConnectionLimitInfo info = kvp.Value;
                writer.WriteStartObject();
                writer.WriteString("Address", kvp.Key.Address ?? "unknown");
                writer.WriteNumber("CurrentConnections", info.CurrentConnections);
                writer.WriteNumber("TotalConnectionsToday", info.TotalConnectionsToday);
                writer.WriteString("LastConnectionUtc", info.LastConnectionTime);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
        finally
        {
            RETURN_SNAPSHOT_TO_POOL(snapshot);
        }
    }

    #endregion Public API

    #region Connection Slot Management

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
        bool isTrustedProxy = false;
        if (_trustedProxies.Count > 0)
        {
            foreach (IPNetwork network in _trustedProxies)
            {
                if (network.Contains(address))
                {
                    isTrustedProxy = true;
                    break;
                }
            }
        }

        int maxConnections = isTrustedProxy ? _proxyConfig.MaxConnectionsPerTrustedProxy : _maxPerEndpoint;
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

            // 4. Runtime ban active -> Reject (Trusted proxies are never banned at runtime)
            if (!isTrustedProxy && bannedUntil > nowTicks)
            {
                this.LOG_BANNED_THROTTLED(entry, key, new DateTime(bannedUntil, DateTimeKind.Utc));

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
            bool shouldTerminateEndpoint = false;
            ConnectionAllowResult result;

            bool spinLockTaken = false;
            try
            {
                entry.SpinLock.Enter(ref spinLockTaken);

                if (entry.IsRemoved)
                {
                    continue; // Retry with a fresh GetOrAdd, this one is tombstoned
                }

                this.TRIM_OLD_TIMESTAMPS(entry.RecentConnectionTimestamps, nowTicks);

                if (entry.RecentConnectionTimestamps.Count >= maxAttempts)
                {
                    // 6. On violation -> Update ban state (if not trusted)
                    if (!isTrustedProxy)
                    {
                        entry.BanCount++;
                        entry.LastBanTimeTicks = nowTicks;

                        TimeSpan banDuration = this.CALCULATE_PROGRESSIVE_BAN_DURATION(entry.BanCount);
                        long banUntilTicks = nowTicks + banDuration.Ticks;
                        _ = Interlocked.Exchange(ref entry.BannedUntilTicks, banUntilTicks);
                        _ = Interlocked.Exchange(ref _persistenceDirty, 1);

                        this.LOG_DDOS_DETECTED_THROTTLED(entry, key);
                        shouldTerminateEndpoint = true;

                        if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                        {
                            DateTime banUntil = new(banUntilTicks, DateTimeKind.Utc);
                            _logger.LogWarning($"[NW.{nameof(ConnectionGuard)}] banned ip={key.Address} count={entry.BanCount} until={banUntil:HH:mm:ss}");
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

            // WHY: Schedule endpoint termination AFTER exiting the lock.
            // Termination closes all connections to this IP — possibly touching ConnectionHub indexes.
            // Nothing prevents ScheduleWorker from running immediately after this line; TaskManager puts it in the queue.
            if (shouldTerminateEndpoint)
            {
                this.SCHEDULE_ENDPOINT_TERMINATION(key);
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
                    _logger.LogDebug($"[NW.{nameof(ConnectionGuard)}] cleared-queue ip={key.Address} reason=oversized");
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

    private void LOG_DDOS_DETECTED_THROTTLED(ConnectionLimitEntry entry, SocketEndpoint key)
    {
        long nowTicks = Clock.NowUtc().Ticks;
        long lastTicks = Interlocked.Read(ref entry.LastDDoSLogTicks);
        long windowTicks = _logSuppressWindowTicks;

        if (nowTicks - lastTicks < windowTicks)
        {
            // Đang trong suppress window -> chỉ đếm, không log
            _ = Interlocked.Increment(ref entry.SuppressedDDoSCount);
            return;
        }

        // Cố gắng "giành quyền" log bằng CAS
        // Chỉ 1 thread thắng, các thread khác tiếp tục bị suppress
        if (Interlocked.CompareExchange(
                ref entry.LastDDoSLogTicks, nowTicks, lastTicks) != lastTicks)
        {
            _ = Interlocked.Increment(ref entry.SuppressedDDoSCount);
            return;
        }

        // Thread thắng CAS -> log summary
        long suppressed = Interlocked.Exchange(ref entry.SuppressedDDoSCount, 0);

        if (suppressed > 0)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    $"[NW.{nameof(ConnectionGuard)}] DDoS-detected ip={key.Address} " +
                    $"(+{suppressed} suppressed-in-last={_protectionConfig.DDoSLogSuppressWindow.TotalSeconds:F0}s)");
            }
        }
        else
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    $"[NW.{nameof(ConnectionGuard)}] DDoS-detected ip={key.Address}");
            }
        }
    }

    /// <summary>
    /// Generic throttled logger. Suppresses repeated messages within a time window.
    /// Returns true nếu nên log (thread thắng CAS), false nếu bị suppress.
    /// </summary>
    /// <param name="lastLogTicks"></param>
    /// <param name="suppressedCount"></param>
    /// <param name="nowTicks"></param>
    /// <param name="windowTicks"></param>
    /// <param name="suppressed"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TRY_ACQUIRE_LOG_SLOT(
        ref long lastLogTicks,
        ref long suppressedCount,
        long nowTicks,
        long windowTicks,
        out long suppressed)
    {
        long lastTicks = Interlocked.Read(ref lastLogTicks);

        if (nowTicks - lastTicks >= windowTicks)
        {
            // Try to acquire log slot
            if (Interlocked.CompareExchange(
                    ref lastLogTicks, nowTicks, lastTicks) == lastTicks)
            {
                suppressed = Interlocked.Exchange(ref suppressedCount, 0);
                return true;
            }
        }

        // Inside window or CAS failed -> suppress
        _ = Interlocked.Increment(ref suppressedCount);

        long newLastTicks = Interlocked.Read(ref lastLogTicks);
        if (nowTicks - newLastTicks >= windowTicks)
        {
            // Window expired during our increment, retry once
            if (Interlocked.CompareExchange(
                    ref lastLogTicks, nowTicks, newLastTicks) == newLastTicks)
            {
                suppressed = Interlocked.Exchange(ref suppressedCount, 0);
                return true;
            }
        }

        suppressed = 0;
        return false;
    }

    private void LOG_BANNED_THROTTLED(ConnectionLimitEntry entry, SocketEndpoint key, DateTime bannedUntil)
    {
        long nowTicks = Clock.NowUtc().Ticks;
        long windowTicks = _logSuppressWindowTicks;

        if (TRY_ACQUIRE_LOG_SLOT(
                ref entry.LastRejectLogTicks,
                ref entry.SuppressedRejectCount,
                nowTicks, windowTicks,
                out long suppressed))
        {
            string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"[NW.{nameof(ConnectionGuard)}] banned-reject ip={key.Address} " +
                                 $"until={bannedUntil:HH:mm:ss}{suffix}");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TRIGGER_ENDPOINT_TERMINATION_UPSTREAM(SocketEndpoint key) => _onEndpointTerminationRequested?.Invoke(key);

    #endregion Connection Slot Management

    #region Report Generation

    /// <summary>
    /// Collects a point-in-time snapshot of all tracked endpoints.
    /// Reads Info under lock for consistency.
    /// </summary>
    private List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> COLLECT_SNAPSHOT()
    {
        int estimatedCapacity = Math.Clamp(_map.Count, MinReportCapacity, MaxReportCapacity);

        ListPool<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> pool = ListPool<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>>.Instance;
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot = pool.Rent(minimumCapacity: estimatedCapacity);

        foreach (KeyValuePair<SocketEndpoint, ConnectionLimitEntry> kvp in _map)
        {
            ConnectionLimitInfo info;
            bool lockTaken = false;
            try
            {
                kvp.Value.SpinLock.Enter(ref lockTaken);
                info = kvp.Value.Info;
            }
            finally
            {
                if (lockTaken)
                {
                    kvp.Value.SpinLock.Exit();
                }
            }
            snapshot.Add(new KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>(kvp.Key, info));
        }

        return snapshot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SORT_SNAPSHOT_BY_LOAD(
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        snapshot.Sort(static (a, b) =>
        {
            int byCurrent = b.Value.CurrentConnections.CompareTo(a.Value.CurrentConnections);
            return byCurrent != 0 ? byCurrent : b.Value.TotalConnectionsToday.CompareTo(a.Value.TotalConnectionsToday);
        });
    }

    private string BUILD_REPORT(List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        GlobalMetrics metrics = this.CALCULATE_GLOBAL_METRICS(snapshot);
        StringBuilder sb = new(512);
        this.APPEND_REPORT_HEADER(sb, metrics);
        APPEND_CONNECTION_DETAILS(sb, snapshot);
        return sb.ToString();
    }

    private readonly struct GlobalMetrics
    {
        public int TotalEndpoints { get; init; }
        public int TotalConcurrent { get; init; }
        public long TotalAttempts { get; init; }
        public long TotalRejections { get; init; }
        public long TotalCleaned { get; init; }
    }

    private GlobalMetrics CALCULATE_GLOBAL_METRICS(List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        int totalConcurrent = 0;
        foreach (KeyValuePair<INetworkEndpoint, ConnectionLimitInfo> kvp in snapshot)
        {
            totalConcurrent += kvp.Value.CurrentConnections;
        }

        return new GlobalMetrics
        {
            TotalEndpoints = snapshot.Count,
            TotalConcurrent = totalConcurrent,
            TotalAttempts = Interlocked.Read(ref _totalConnectionAttempts),
            TotalRejections = Interlocked.Read(ref _totalRejections),
            TotalCleaned = Interlocked.Read(ref _totalCleanedEntries)
        };
    }

    private void APPEND_REPORT_HEADER(StringBuilder sb, GlobalMetrics metrics)
    {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{Clock.NowUtc():yyyy-MM-dd HH:mm:ss}] ConnectionGuard Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MaxPerEndpoint     : {_maxPerEndpoint}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CleanupInterval    : {_cleanupInterval.TotalSeconds:F0}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"InactivityThreshold: {_inactivityThreshold.TotalSeconds:F0}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TrackedEndpoints   : {metrics.TotalEndpoints}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalConcurrent    : {metrics.TotalConcurrent}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalAttempts      : {metrics.TotalAttempts:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalRejections    : {metrics.TotalRejections:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalCleaned       : {metrics.TotalCleaned:N0}");

        if (metrics.TotalAttempts > 0)
        {
            double rejectionRate = metrics.TotalRejections * 100.0 / metrics.TotalAttempts;
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RejectionRate      : {rejectionRate:F2}%");
        }

        _ = sb.AppendLine();
    }

    private static void APPEND_CONNECTION_DETAILS(StringBuilder sb, List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        _ = sb.AppendLine("Top Endpoints by CurrentConnections:");
        _ = sb.AppendLine("---------------------------------------------------------------");
        _ = sb.AppendLine("Endpoint                   | Current | Today     | LastUtc     ");
        _ = sb.AppendLine("---------------------------------------------------------------");

        if (snapshot.Count == 0)
        {
            _ = sb.AppendLine("(no tracked endpoints)");
        }
        else
        {
            APPEND_TOP_ENDPOINTS(sb, snapshot, maxRows: 100);
        }

        _ = sb.AppendLine("---------------------------------------------------------------");
    }

    private static void APPEND_TOP_ENDPOINTS(StringBuilder sb, List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot, int maxRows)
    {
        int rows = 0;

        foreach (KeyValuePair<INetworkEndpoint, ConnectionLimitInfo> kvp in snapshot)
        {
            if (rows++ >= maxRows)
            {
                break;
            }

            string address = kvp.Key.Address ?? "unknown";
            ConnectionLimitInfo info = kvp.Value;

            string addressCol = address.Length > 27
                ? $"{address[..27]}\u2026"
                : address.PadRight(27);

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{addressCol} | {info.CurrentConnections,7} | {info.TotalConnectionsToday,9} | {info.LastConnectionTime:u}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RETURN_SNAPSHOT_TO_POOL(List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        ListPool<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>>.Instance
            .Return(snapshot, clearItems: true);
    }

    #endregion Report Generation

    #region Cleanup

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RUN_CLEANUP_ONCE()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            DateTime cutoff = Clock.NowUtc() - _inactivityThreshold;
            int scanned = 0;
            int removed = 0;

            int maxCleanupKeys = _config.MaxCleanupKeysPerRun > 0
                ? _config.MaxCleanupKeysPerRun
                : Math.Max(1000, _map.Count / 4);

            List<SocketEndpoint> keysToRemove = new(Math.Min(maxCleanupKeys, _map.Count));

            foreach (KeyValuePair<SocketEndpoint, ConnectionLimitEntry> kvp in _map)
            {
                if (scanned++ >= maxCleanupKeys)
                {
                    break;
                }

                bool lockTaken = false;
                bool shouldRemove = false;

                try
                {
                    kvp.Value.SpinLock.Enter(ref lockTaken);
                    shouldRemove = SHOULD_REMOVE_ENTRY(kvp.Value, cutoff);
                }
                finally
                {
                    if (lockTaken)
                    {
                        kvp.Value.SpinLock.Exit();
                    }
                }

                if (shouldRemove)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            // Remove in separate pass to avoid holding locks
            foreach (SocketEndpoint key in keysToRemove)
            {
                if (_map.TryGetValue(key, out ConnectionLimitEntry? entry) && entry is not null)
                {
                    bool lockTaken = false;
                    bool canRemove = false;
                    try
                    {
                        entry.SpinLock.Enter(ref lockTaken);
                        // Double check under lock to prevent TOCTOU
                        if (SHOULD_REMOVE_ENTRY(entry, cutoff))
                        {
                            entry.IsRemoved = true;
                            canRemove = true;
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            entry.SpinLock.Exit();
                        }
                    }

                    if (canRemove)
                    {
                        if (_map.TryRemove(key, out ConnectionLimitEntry? removedEntry) && removedEntry is not null)
                        {
                            // Clear queue without lock since no one else can acquire it
                            removedEntry.RecentConnectionTimestamps.Clear();
                            removed++;
                            _ = Interlocked.Increment(ref _totalCleanedEntries);
                        }
                    }
                }
            }

            if (removed > 0)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"[NW.{nameof(ConnectionGuard)}] cleanup scanned={scanned} removed={removed} remaining={_map.Count}");
                }
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.{nameof(ConnectionGuard)}] cleanup-error");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SHOULD_REMOVE_ENTRY(ConnectionLimitEntry entry, DateTime cutoff)
    {
        long bannedUntil = Interlocked.Read(ref entry.BannedUntilTicks);
        if (bannedUntil > cutoff.Ticks)
        {
            return false;
        }

        // Read Info without lock — approximate check is fine for cleanup decisions.
        ConnectionLimitInfo info = entry.Info;
        return info.CurrentConnections <= 0 && info.LastConnectionTime < cutoff;
    }

    #endregion Cleanup

    #region Initialization

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void INITIALIZE_METRICS()
    {
        _totalRejections = 0;
        _totalCleanedEntries = 0;
        _totalConnectionAttempts = 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SCHEDULE_CLEANUP_JOB()
    {
        TaskManager taskManager = InstanceManager.Instance.GetOrCreateInstance<TaskManager>();

        _ = taskManager.ScheduleRecurring(
            name: TaskNaming.Recurring.CleanupJobId(RecurringName, this.GetHashCode()),
            interval: _cleanupInterval,
            work: _ =>
            {
                this.RUN_CLEANUP_ONCE();
                return ValueTask.CompletedTask;
            },
            options: new RecurringOptions
            {
                NonReentrant = true,
                Tag = TaskNaming.Tags.Service,
                BackoffCap = TimeSpan.FromSeconds(15),
                Jitter = TimeSpan.FromMilliseconds(250),
                ExecutionTimeout = TimeSpan.FromSeconds(2)
            }
        );

        if (_storeConfig.Enabled)
        {
            _ = taskManager.ScheduleRecurring(
                name: TaskNaming.Recurring.CleanupJobId(RecurringName + ".save", this.GetHashCode()),
                interval: _storeConfig.AutoSaveInterval,
                work: _ =>
                {
                    this.SAVE_BANNED_IPS();
                    return ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
                    ExecutionTimeout = TimeSpan.FromSeconds(10)
                }
            );
        }
    }

    private void SAVE_BANNED_IPS()
    {
        if (Interlocked.CompareExchange(ref _persistenceDirty, 0, 1) == 0)
        {
            return; // No changes to save
        }

        try
        {
            List<ConnectionBanRecord> snapshot = new();
            DateTime now = Clock.NowUtc();

            // Minimal lock to collect snapshot
            foreach (KeyValuePair<SocketEndpoint, ConnectionLimitEntry> kvp in _map)
            {
                long bannedUntil = Interlocked.Read(ref kvp.Value.BannedUntilTicks);
                int banCount = kvp.Value.BanCount;

                // Only save entries that are currently banned or have a progressive ban count (to retain decay state)
                if (bannedUntil > now.Ticks || banCount > 0)
                {
                    bool lockTaken = false;
                    long lastBanTime;
                    long lastSeen;

                    try
                    {
                        kvp.Value.SpinLock.Enter(ref lockTaken);
                        lastBanTime = kvp.Value.LastBanTimeTicks;
                        lastSeen = Interlocked.Read(ref kvp.Value.LastSeenAtTicks);
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            kvp.Value.SpinLock.Exit();
                        }
                    }

                    snapshot.Add(new ConnectionBanRecord(kvp.Key, bannedUntil, banCount, lastBanTime, lastSeen));
                }
            }

            if (snapshot.Count > 0)
            {
                string path = System.IO.Path.Combine(Directories.DataDirectory, _storeConfig.StoreFileName);
                ConnectionBanStore.Save(path, snapshot, snapshot.Count);

                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"[NW.{nameof(ConnectionGuard)}] Persisted {snapshot.Count} bans to disk.");
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            // Restore dirty flag if save failed
            _ = Interlocked.Exchange(ref _persistenceDirty, 1);
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.{nameof(ConnectionGuard)}] failed to save banned ips.");
            }
        }
    }

    #endregion Initialization

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
            TaskManager? taskManager = InstanceManager.Instance.GetExistingInstance<TaskManager>();

            taskManager?.CancelRecurring(TaskNaming.Recurring.CleanupJobId(RecurringName, this.GetHashCode()));
            taskManager?.CancelRecurring(TaskNaming.Recurring.CleanupJobId(RecurringName + ".save", this.GetHashCode()));

            if (_storeConfig.Enabled)
            {
                this.SAVE_BANNED_IPS(); // Save snapshot BEFORE clearing the map
            }

            _map.Clear();

            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug($"[NW.{nameof(ConnectionGuard)}:{nameof(Dispose)}] disposed");
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.{nameof(ConnectionGuard)}:{nameof(Dispose)}] dispose-error");
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

    #region Internal Types

    internal readonly struct ConnectionAllowResult
    {
        public bool Allowed { get; init; }
        public int CurrentConnections { get; init; }
    }

    /// <summary>
    /// Immutable snapshot of connection tracking data for an endpoint.
    /// Used as the value type for CAS-style updates within a locked <see cref="ConnectionLimitEntry"/>.
    /// </summary>
    [DebuggerDisplay("Current={CurrentConnections}, Today={TotalConnectionsToday}, Last={LastConnectionTime}")]
    internal readonly record struct ConnectionLimitInfo
    {
        /// <summary>Current number of active connections.</summary>
        public int CurrentConnections { get; init; }

        /// <summary>Timestamp of most recent connection activity.</summary>
        public DateTime LastConnectionTime { get; init; }

        /// <summary>Total connections established today (resets daily).</summary>
        public int TotalConnectionsToday { get; init; }

        public ConnectionLimitInfo(
            int currentConnections,
            DateTime lastConnectionTime,
            int totalConnectionsToday)
        {
            this.CurrentConnections = currentConnections;
            this.LastConnectionTime = lastConnectionTime;
            this.TotalConnectionsToday = totalConnectionsToday;
        }
    }

    /// <summary>
    /// Mutable container for one endpoint's tracking state.
    /// <para>
    /// <see cref="Info"/> is a value-type snapshot; mutations must be done inside
    /// <c>lock(entry)</c> to avoid torn reads/writes under concurrent access.
    /// </para>
    /// <para>
    /// <see cref="RecentConnectionTimestamps"/> is a <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/>
    /// and can be trimmed lock-free; enqueues happen inside the lock alongside the Info update.
    /// </para>
    /// </summary>
    internal sealed class ConnectionLimitEntry
    {
        public bool IsRemoved;
        public long BannedUntilTicks;

        /// <summary>
        /// Track the progressive ban tier.
        /// </summary>
        public int BanCount;

        /// <summary>
        /// Last time this IP was banned. Used for ban count decay.
        /// </summary>
        public long LastBanTimeTicks;

        /// <summary>
        /// Last time any network activity was seen from this IP.
        /// </summary>
        public long LastSeenAtTicks;

        /// <summary>
        /// lần cuối log DDoS warn
        /// </summary>
        public long LastDDoSLogTicks;
        /// <summary>
        /// số lần bị suppress
        /// </summary>
        public long SuppressedDDoSCount;

        /// <summary>
        /// Reject log throttle (new)
        /// </summary>
        public long LastRejectLogTicks;
        public long SuppressedRejectCount;

        /// <summary>
        /// Closed log throttle (new)
        /// </summary>
        public long LastClosedLogTicks;
        public long SuppressedClosedCount;

        /// <summary>
        /// Mutable connection info. Access only inside <c>SpinLock</c>.
        /// </summary>
        public ConnectionLimitInfo Info;

        /// <summary>
        /// SpinLock for micro-operations. Faster than Monitor.
        /// </summary>
        public SpinLock SpinLock = new(false);

        /// <summary>
        /// Sliding-window timestamps for rate limiting.
        /// </summary>
        public readonly System.Collections.Generic.Queue<long> RecentConnectionTimestamps = new();
    }

    #endregion Internal Types
}
