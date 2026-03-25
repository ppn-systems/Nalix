// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.Network.Configurations;
using Nalix.Network.Connections;
using Nalix.Network.Internal;
using Nalix.Shared.Memory.Pools;

namespace Nalix.Network.Throttling;

/// <summary>
/// High-performance per-endpoint concurrent connection limiter.
/// Uses a hybrid approach: a sealed class entry (<see cref="ConnectionLimitEntry"/>) holds
/// a mutable <see cref="ConnectionLimitInfo"/> struct protected by a per-entry lock,
/// plus a <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> for rate-window tracking.
/// Supports automatic cleanup of stale entries to bound memory usage.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class ConnectionLimiter : System.IDisposable, System.IAsyncDisposable, IReportable
{
    #region Constants

    private const int MinReportCapacity = 128;
    private const int MaxReportCapacity = 4096;
    private const int MaxCleanupKeysPerRun = 1000;

    #endregion Constants

    #region Fields

    private readonly int _maxPerEndpoint;
    private readonly ConnectionLimitOptions _config;
    private readonly System.TimeSpan _cleanupInterval;
    private readonly System.TimeSpan _inactivityThreshold;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<INetworkEndpoint, ConnectionLimitEntry> _map;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    private int _disposed;

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

    static ConnectionLimiter() => RecurringName = "conn.limit";

    /// <summary>
    /// Initializes a new <see cref="ConnectionLimiter"/> with optional configuration.
    /// </summary>
    /// <param name="config">Configuration options. If null, uses global configuration.</param>
    /// <exception cref="InternalErrorException">Thrown when configuration validation fails.</exception>
    public ConnectionLimiter([System.Diagnostics.CodeAnalysis.AllowNull] ConnectionLimitOptions config = null)
    {
        _config = config ?? ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
        _config.Validate();

        _maxPerEndpoint = _config.MaxConnectionsPerIpAddress;
        _cleanupInterval = _config.CleanupInterval;
        _inactivityThreshold = _config.InactivityThreshold;

        _map = new System.Collections.Concurrent.ConcurrentDictionary<INetworkEndpoint, ConnectionLimitEntry>();

        INITIALIZE_METRICS();
        SCHEDULE_CLEANUP_JOB();

        s_logger?.Debug($"[NW.{nameof(ConnectionLimiter)}] init " +
                       $"maxPerEndpoint={_maxPerEndpoint} " +
                       $"inactivity={_inactivityThreshold.TotalSeconds:F0}s " +
                       $"cleanup={_cleanupInterval.TotalSeconds:F0}s");
    }

    /// <summary>Initializes a new <see cref="ConnectionLimiter"/> using global configuration.</summary>
    public ConnectionLimiter() : this(config: null) { }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Attempts to acquire a connection slot for the given endpoint.
    /// </summary>
    /// <param name="endPoint">The IP endpoint requesting connection.</param>
    /// <returns>True if connection is allowed; false if limit exceeded.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if limiter is disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown if endPoint is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public bool IsConnectionAllowed(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPEndPoint endPoint)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) != 0, nameof(ConnectionLimiter));

        if (endPoint is null)
        {
            throw new InternalErrorException("NetworkEndpoint cannot be null", nameof(endPoint));
        }

        _ = SAFE_INCREMENT(ref _totalConnectionAttempts);

        System.DateTime now = Clock.NowUtc();
        INetworkEndpoint key = CONVERT_TO_NETWORK_ENDPOINT(endPoint);

        ConnectionAllowResult result = TRY_ACQUIRE_CONNECTION_SLOT(key, now);

        if (!result.Allowed)
        {
            _ = System.Threading.Interlocked.Increment(ref _totalRejections);

            // Throttled reject log — chỉ log 1 lần mỗi suppress window per IP
            if (_map.TryGetValue(key, out ConnectionLimitEntry entry))
            {
                long nowTicks = Clock.NowUtc().Ticks;
                long windowTicks = _config.DDoSLogSuppressWindow.Ticks;

                if (TRY_ACQUIRE_LOG_SLOT(
                        ref entry.LastRejectLogTicks,
                        ref entry.SuppressedRejectCount,
                        nowTicks, windowTicks,
                        out long suppressed))
                {
                    string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

                    s_logger?.Info(
                        $"[NW.{nameof(ConnectionLimiter)}] reject endpoint={endPoint} " +
                        $"current={result.CurrentConnections} limit={_maxPerEndpoint}{suffix}");
                }
            }
        }
        else
        {
            s_logger?.Trace($"[NW.{nameof(ConnectionLimiter)}] allow endpoint={endPoint} current={result.CurrentConnections} limit={_maxPerEndpoint}");
        }

        return result.Allowed;
    }

    /// <summary>
    /// Attempts to acquire a connection slot for the given endpoint.
    /// </summary>
    /// <param name="endPoint">The endpoint requesting connection.</param>
    /// <returns>True if connection is allowed; false if limit exceeded.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if limiter is disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown if endPoint is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool IsConnectionAllowed(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.EndPoint endPoint)
    {
        if (endPoint is null)
        {
            throw new InternalErrorException("NetworkEndpoint cannot be null", nameof(endPoint));
        }

        if (endPoint is not System.Net.IPEndPoint ipEndPoint)
        {
            s_logger?.Warn($"[NW.{nameof(ConnectionLimiter)}] received non-IPEndPoint ({endPoint.GetType().Name}) - rejecting");
            return false;
        }

        return IsConnectionAllowed(ipEndPoint);
    }

    /// <summary>
    /// Handles connection closure event and decrements the connection counter.
    /// </summary>
    /// <param name="sender">Event sender (unused).</param>
    /// <param name="args">Connection event arguments.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Event handler signature")]
    public void OnConnectionClosed(
        [System.Diagnostics.CodeAnalysis.AllowNull] object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs args)
    {
        if (System.Threading.Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (args?.Connection?.NetworkEndpoint is null)
        {
            s_logger?.Warn($"[NW.{nameof(ConnectionLimiter)}:Internal] received-null args/connection/endpoint");
            return;
        }

        if (string.IsNullOrWhiteSpace(args.Connection.NetworkEndpoint.Address))
        {
            s_logger?.Warn($"[NW.{nameof(ConnectionLimiter)}:Internal] received-empty-address");
            return;
        }

        System.DateTime now = Clock.NowUtc();
        Connection.Endpoint key = Connection.Endpoint.FromIpAddress(
            System.Net.IPAddress.Parse(args.Connection.NetworkEndpoint.Address)
        );

        bool released = TRY_RELEASE_CONNECTION_SLOT(key, now);

        if (released && _map.TryGetValue(key, out ConnectionLimitEntry closedEntry))
        {
            long nowTicks = Clock.NowUtc().Ticks;
            long windowTicks = _config.DDoSLogSuppressWindow.Ticks;

            if (TRY_ACQUIRE_LOG_SLOT(
                    ref closedEntry.LastClosedLogTicks,
                    ref closedEntry.SuppressedClosedCount,
                    nowTicks, windowTicks,
                    out long suppressed))
            {
                string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

                s_logger?.Trace($"[NW.{nameof(ConnectionLimiter)}] closed endpoint={key.Address}{suffix}");
            }
        }
    }

    /// <summary>
    /// Generates a human-readable diagnostic report of connection limiter state.
    /// </summary>
    /// <returns>Formatted report string.</returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        List<
            KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot = COLLECT_SNAPSHOT();

        try
        {
            SORT_SNAPSHOT_BY_LOAD(snapshot);
            return BUILD_REPORT(snapshot);
        }
        finally
        {
            RETURN_SNAPSHOT_TO_POOL(snapshot);
        }
    }

    #endregion Public API

    #region Connection Slot Management

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "Interface required")]
    private static INetworkEndpoint CONVERT_TO_NETWORK_ENDPOINT(System.Net.IPEndPoint endPoint)
        => Connection.Endpoint.FromIpAddress(endPoint.Address);

    /// <summary>
    /// Attempts to acquire a connection slot.
    /// Uses GetOrAdd to safely retrieve-or-create the entry, then locks the entry
    /// for the counter mutation. The rate-window queue is trimmed before the check.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="now"></param>
    private ConnectionAllowResult TRY_ACQUIRE_CONNECTION_SLOT(INetworkEndpoint key, System.DateTime now)
    {
        // GetOrAdd is atomic w.r.t. insertion; the returned entry is always the canonical one.
        ConnectionLimitEntry entry = _map.GetOrAdd(key, static _ => new ConnectionLimitEntry());

        long bannedUntil = System.Threading.Interlocked.Read(ref entry.BannedUntilTicks);
        if (bannedUntil > now.Ticks)
        {
            LOG_BANNED_THROTTLED(entry, key, new System.DateTime(bannedUntil, System.DateTimeKind.Utc));

            int currentConns;
            lock (entry)
            {
                currentConns = entry.Info.CurrentConnections;
            }

            return new ConnectionAllowResult
            {
                Allowed = false,
                CurrentConnections = currentConns
            };
        }

        // Lock the entry to safely mutate Info and enqueue timestamp atomically.
        lock (entry)
        {
            // Trim expired timestamps (lock-free – ConcurrentQueue is thread-safe).
            TRIM_OLD_TIMESTAMPS(entry.RecentConnectionTimestamps, now);

            // Re-check rate window under lock (could have changed between outer check and lock).
            if (entry.RecentConnectionTimestamps.Count >= _config.MaxConnectionsPerWindow)
            {
                System.DateTime banUntil = now + _config.BanDuration;
                _ = System.Threading.Interlocked.Exchange(ref entry.BannedUntilTicks, banUntil.Ticks);

                LOG_DDOS_DETECTED_THROTTLED(entry, key);

                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                    name: $"{TaskNaming.Tags.Worker}.{TaskNaming.Tags.Process}",
                    group: $"{TaskNaming.Tags.Worker}/",
                    work: async (_, _) =>
                    {
                        _ = InstanceManager.Instance.GetOrCreateInstance<ConnectionHub>()
                                                .ForceClose(key);
                    },
                    options: new WorkerOptions
                    {
                        Tag = NetTaskNames.Net,
                        IdType = SnowflakeType.System,
                        RetainFor = System.TimeSpan.Zero,
                    }
                );

                s_logger?.Warn($"[NW.{nameof(ConnectionLimiter)}] banned ip={key.Address} until={banUntil:HH:mm:ss}");

                return new ConnectionAllowResult
                {
                    Allowed = false,
                    CurrentConnections = entry.Info.CurrentConnections
                };
            }

            int newTotalToday = CALCULATE_TOTAL_CONNECTIONS_TODAY(entry.Info, now.Date);

            entry.Info = entry.Info with
            {
                CurrentConnections = entry.Info.CurrentConnections + 1,
                TotalConnectionsToday = newTotalToday,
                LastConnectionTime = now
            };

            entry.RecentConnectionTimestamps.Enqueue(now);

            return new ConnectionAllowResult { Allowed = true, CurrentConnections = entry.Info.CurrentConnections };
        }
    }

    /// <summary>Removes timestamps outside the rate-window. Lock-free — ConcurrentQueue is safe.</summary>
    /// <param name="timestamps"></param>
    /// <param name="now"></param>
    private void TRIM_OLD_TIMESTAMPS(
        System.Collections.Concurrent.ConcurrentQueue<System.DateTime> timestamps,
        System.DateTime now)
    {
        System.DateTime cutoff = now - _config.ConnectionRateWindow;

        while (timestamps.TryPeek(out System.DateTime oldest) && oldest < cutoff)
        {
            // Check TryDequeue result to handle race
            if (!timestamps.TryDequeue(out System.DateTime dequeued))
            {
                break; // Another thread dequeued it
            }

            // Double-check dequeued value is still old
            if (dequeued >= cutoff)
            {
                // Race: someone enqueued between peek and dequeue
                // Re-enqueue it (FIFO order maintained)
                timestamps.Enqueue(dequeued);
                break;
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int CALCULATE_TOTAL_CONNECTIONS_TODAY(ConnectionLimitInfo info, System.DateTime today)
    {
        if (info.LastConnectionTime == default)
        {
            return 1;
        }

        // Use Date comparison to avoid timezone issues
        if (info.LastConnectionTime.Date < today)
        {
            return 1; // New day, reset counter
        }

        // Prevent overflow
        return info.TotalConnectionsToday >= int.MaxValue - 1 ? int.MaxValue : info.TotalConnectionsToday + 1;
    }

    /// <summary>
    /// Releases a connection slot for the given endpoint.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="now"></param>
    private bool TRY_RELEASE_CONNECTION_SLOT(Connection.Endpoint key, System.DateTime now)
    {
        if (!_map.TryGetValue(key, out ConnectionLimitEntry entry))
        {
            return false;
        }

        lock (entry)
        {
            // Decrement with underflow protection
            int newCount = System.Math.Max(0, entry.Info.CurrentConnections - 1);

            entry.Info = entry.Info with
            {
                CurrentConnections = newCount,
                LastConnectionTime = now
            };

            // Trim queue when releasing to prevent unbounded growth
            if (newCount == 0)
            {
                TRIM_OLD_TIMESTAMPS(entry.RecentConnectionTimestamps, now);

                // Clear queue if no connections and queue is large
                if (entry.RecentConnectionTimestamps.Count > _config.MaxConnectionsPerWindow * 2)
                {
                    entry.RecentConnectionTimestamps.Clear();

                    s_logger?.Debug($"[NW.{nameof(ConnectionLimiter)}] cleared-queue ip={key.Address} reason=oversized");
                }
            }
        }

        return true;
    }

    private void LOG_DDOS_DETECTED_THROTTLED(ConnectionLimitEntry entry, INetworkEndpoint key)
    {
        long nowTicks = Clock.NowUtc().Ticks;
        long lastTicks = System.Threading.Interlocked.Read(ref entry.LastDDoSLogTicks);
        long windowTicks = _config.DDoSLogSuppressWindow.Ticks;

        if (nowTicks - lastTicks < windowTicks)
        {
            // Đang trong suppress window → chỉ đếm, không log
            _ = System.Threading.Interlocked.Increment(ref entry.SuppressedDDoSCount);
            return;
        }

        // Cố gắng "giành quyền" log bằng CAS
        // Chỉ 1 thread thắng, các thread khác tiếp tục bị suppress
        if (System.Threading.Interlocked.CompareExchange(
                ref entry.LastDDoSLogTicks, nowTicks, lastTicks) != lastTicks)
        {
            _ = System.Threading.Interlocked.Increment(ref entry.SuppressedDDoSCount);
            return;
        }

        // Thread thắng CAS → log summary
        long suppressed = System.Threading.Interlocked.Exchange(ref entry.SuppressedDDoSCount, 0);

        if (suppressed > 0)
        {
            s_logger?.Warn(
                $"[NW.{nameof(ConnectionLimiter)}] DDoS-detected ip={key.Address} " +
                $"(+{suppressed} suppressed-in-last={_config.DDoSLogSuppressWindow.TotalSeconds:F0}s)");
        }
        else
        {
            s_logger?.Warn(
                $"[NW.{nameof(ConnectionLimiter)}] DDoS-detected ip={key.Address}");
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool TRY_ACQUIRE_LOG_SLOT(
        ref long lastLogTicks,
        ref long suppressedCount,
        long nowTicks,
        long windowTicks,
        out long suppressed)
    {
        long lastTicks = System.Threading.Interlocked.Read(ref lastLogTicks);

        if (nowTicks - lastTicks >= windowTicks)
        {
            // Try to acquire log slot
            if (System.Threading.Interlocked.CompareExchange(
                    ref lastLogTicks, nowTicks, lastTicks) == lastTicks)
            {
                suppressed = System.Threading.Interlocked.Exchange(ref suppressedCount, 0);
                return true;
            }
        }

        // Inside window or CAS failed → suppress
        _ = System.Threading.Interlocked.Increment(ref suppressedCount);

        long newLastTicks = System.Threading.Interlocked.Read(ref lastLogTicks);
        if (nowTicks - newLastTicks >= windowTicks)
        {
            // Window expired during our increment, retry once
            if (System.Threading.Interlocked.CompareExchange(
                    ref lastLogTicks, nowTicks, newLastTicks) == newLastTicks)
            {
                suppressed = System.Threading.Interlocked.Exchange(ref suppressedCount, 0);
                return true;
            }
        }

        suppressed = 0;
        return false;
    }

    private void LOG_BANNED_THROTTLED(ConnectionLimitEntry entry, INetworkEndpoint key, System.DateTime bannedUntil)
    {
        long nowTicks = Clock.NowUtc().Ticks;
        long windowTicks = _config.DDoSLogSuppressWindow.Ticks;

        if (TRY_ACQUIRE_LOG_SLOT(
                ref entry.LastRejectLogTicks,
                ref entry.SuppressedRejectCount,
                nowTicks, windowTicks,
                out long suppressed))
        {
            string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

            s_logger?.Trace($"[NW.{nameof(ConnectionLimiter)}] banned-reject ip={key.Address} " +
                           $"until={bannedUntil:HH:mm:ss}{suffix}");
        }
    }

    #endregion Connection Slot Management

    #region Report Generation

    /// <summary>
    /// Collects a point-in-time snapshot of all tracked endpoints.
    /// Reads Info under lock for consistency.
    /// </summary>
    private List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> COLLECT_SNAPSHOT()
    {
        int estimatedCapacity = System.Math.Clamp(_map.Count, MinReportCapacity, MaxReportCapacity);

        ListPool<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> pool = ListPool<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>>.Instance;
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot = pool.Rent(minimumCapacity: estimatedCapacity);

        foreach (KeyValuePair<INetworkEndpoint, ConnectionLimitEntry> kvp in _map)
        {
            ConnectionLimitInfo info;
            lock (kvp.Value)
            {
                info = kvp.Value.Info;
            }
            snapshot.Add(new KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>(kvp.Key, info));
        }

        return snapshot;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void SORT_SNAPSHOT_BY_LOAD(
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        snapshot.Sort(static (a, b) =>
        {
            int byCurrent = b.Value.CurrentConnections.CompareTo(a.Value.CurrentConnections);
            return byCurrent != 0 ? byCurrent : b.Value.TotalConnectionsToday.CompareTo(a.Value.TotalConnectionsToday);
        });
    }

    private string BUILD_REPORT(
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        GlobalMetrics metrics = CALCULATE_GLOBAL_METRICS(snapshot);
        System.Text.StringBuilder sb = new(512);
        APPEND_REPORT_HEADER(sb, metrics);
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

    private GlobalMetrics CALCULATE_GLOBAL_METRICS(
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
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
            TotalAttempts = System.Threading.Interlocked.Read(ref _totalConnectionAttempts),
            TotalRejections = System.Threading.Interlocked.Read(ref _totalRejections),
            TotalCleaned = System.Threading.Interlocked.Read(ref _totalCleanedEntries)
        };
    }

    private void APPEND_REPORT_HEADER(System.Text.StringBuilder sb, GlobalMetrics metrics)
    {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{Clock.NowUtc():yyyy-MM-dd HH:mm:ss}] ConnectionLimiter Status:");
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

    private static void APPEND_CONNECTION_DETAILS(
        System.Text.StringBuilder sb,
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
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

    private static void APPEND_TOP_ENDPOINTS(
        System.Text.StringBuilder sb,
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot, int maxRows)
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void RETURN_SNAPSHOT_TO_POOL(
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        ListPool<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>>.Instance
            .Return(snapshot, clearItems: true);
    }

    #endregion Report Generation

    #region Cleanup

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void RUN_CLEANUP_ONCE()
    {
        if (System.Threading.Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            System.DateTime cutoff = Clock.NowUtc() - _inactivityThreshold;
            int scanned = 0;
            int removed = 0;

            List<INetworkEndpoint> keysToRemove = new(MaxCleanupKeysPerRun);

            foreach (KeyValuePair<INetworkEndpoint, ConnectionLimitEntry> kvp in _map)
            {
                if (scanned++ >= MaxCleanupKeysPerRun)
                {
                    break;
                }

                bool shouldRemove;
                lock (kvp.Value)
                {
                    shouldRemove = SHOULD_REMOVE_ENTRY(kvp.Value, cutoff);
                }

                if (shouldRemove)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            // Remove in separate pass to avoid holding locks
            foreach (INetworkEndpoint key in keysToRemove)
            {
                if (_map.TryRemove(key, out ConnectionLimitEntry removedEntry))
                {
                    // Dispose resources
                    lock (removedEntry)
                    {
                        removedEntry.RecentConnectionTimestamps.Clear();
                    }

                    removed++;
                    _ = System.Threading.Interlocked.Increment(ref _totalCleanedEntries);
                }
            }

            if (removed > 0)
            {
                s_logger?.Debug($"[NW.{nameof(ConnectionLimiter)}] cleanup scanned={scanned} removed={removed} remaining={_map.Count}");
            }
        }
        catch (System.Exception ex) when (ex is not System.ObjectDisposedException)
        {
            s_logger?.Error($"[NW.{nameof(ConnectionLimiter)}] cleanup-error", ex);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool SHOULD_REMOVE_ENTRY(ConnectionLimitEntry entry, System.DateTime cutoff)
    {
        long bannedUntil = System.Threading.Interlocked.Read(ref entry.BannedUntilTicks);
        if (bannedUntil > cutoff.Ticks)
        {
            return false;
        }

        // Read Info without lock — approximate check is fine for cleanup decisions.
        ConnectionLimitInfo info = entry.Info;
        return info.CurrentConnections <= 0 && info.LastConnectionTime < cutoff;
    }

    /// <summary>
    /// Safely increments a counter with overflow protection.
    /// </summary>
    /// <param name="counter"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static long SAFE_INCREMENT(ref long counter)
    {
        while (true)
        {
            long current = System.Threading.Interlocked.Read(ref counter);

            if (current >= long.MaxValue - 1)
            {
                // Reset to reasonable value instead of wrapping
                _ = System.Threading.Interlocked.CompareExchange(ref counter, 1_000_000, current);
                return 1_000_000;
            }

            long next = current + 1;
            if (System.Threading.Interlocked.CompareExchange(ref counter, next, current) == current)
            {
                return next;
            }

            // Retry on race
            System.Threading.Thread.SpinWait(1);
        }
    }

    #endregion Cleanup

    #region Initialization

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void INITIALIZE_METRICS()
    {
        _totalRejections = 0;
        _totalCleanedEntries = 0;
        _totalConnectionAttempts = 0;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void SCHEDULE_CLEANUP_JOB()
    {
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: TaskNaming.Recurring.CleanupJobId(RecurringName, GetHashCode()),
            interval: _cleanupInterval,
            work: _ =>
            {
                RUN_CLEANUP_ONCE();
                return System.Threading.Tasks.ValueTask.CompletedTask;
            },
            options: new RecurringOptions
            {
                NonReentrant = true,
                Tag = TaskNaming.Tags.Service,
                BackoffCap = System.TimeSpan.FromSeconds(15),
                Jitter = System.TimeSpan.FromMilliseconds(250),
                ExecutionTimeout = System.TimeSpan.FromSeconds(2)
            }
        );
    }

    #endregion Initialization

    #region IDisposable & IAsyncDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _ = (InstanceManager.Instance.GetExistingInstance<TaskManager>()
                ?.CancelRecurring(TaskNaming.Recurring.CleanupJobId(RecurringName, GetHashCode())));

            _map.Clear();

            s_logger?.Debug($"[NW.{nameof(ConnectionLimiter)}:{nameof(Dispose)}] disposed");
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(ConnectionLimiter)}:{nameof(Dispose)}] dispose-error msg={ex.Message}");
        }

        System.GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public System.Threading.Tasks.ValueTask DisposeAsync()
    {
        Dispose();
        return System.Threading.Tasks.ValueTask.CompletedTask;
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
    [System.Diagnostics.DebuggerDisplay("Current={CurrentConnections}, Today={TotalConnectionsToday}, Last={LastConnectionTime}")]
    internal readonly record struct ConnectionLimitInfo
    {
        /// <summary>Current number of active connections.</summary>
        public int CurrentConnections { get; init; }

        /// <summary>Timestamp of most recent connection activity.</summary>
        public System.DateTime LastConnectionTime { get; init; }

        /// <summary>Total connections established today (resets daily).</summary>
        public int TotalConnectionsToday { get; init; }

        public ConnectionLimitInfo(
            int currentConnections,
            System.DateTime lastConnectionTime,
            int totalConnectionsToday)
        {
            CurrentConnections = currentConnections;
            LastConnectionTime = lastConnectionTime;
            TotalConnectionsToday = totalConnectionsToday;
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
        public long BannedUntilTicks;

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
        /// Mutable connection info. Access only inside <c>lock(this)</c>.
        /// </summary>
        public ConnectionLimitInfo Info;

        /// <summary>
        /// Sliding-window timestamps for rate limiting.
        /// Trim operations are lock-free; enqueue happens under the entry lock.
        /// </summary>
        public readonly System.Collections.Concurrent.ConcurrentQueue<System.DateTime> RecentConnectionTimestamps = new();
    }

    #endregion Internal Types
}
