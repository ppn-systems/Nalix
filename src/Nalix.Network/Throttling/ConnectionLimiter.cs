// Copyright (c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Common.Infrastructure.Connection;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Configurations;
using Nalix.Shared.Memory.Pools;

namespace Nalix.Network.Throttling;

/// <summary>
/// High-performance per-endpoint concurrent connection limiter.
/// Uses lock-free CAS updates with bounded retries to avoid lost updates under contention.
/// Supports automatic cleanup of stale entries to bound memory usage.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class ConnectionLimiter : System.IDisposable, System.IAsyncDisposable, IReportable
{
    #region Constants

    /// <summary>
    /// Maximum number of keys to scan per cleanup run to prevent long pauses.
    /// </summary>
    private const System.Int32 MaxCleanupKeysPerRun = 1000;

    /// <summary>
    /// Maximum CAS retry attempts before failing the operation.
    /// </summary>
    private const System.Int32 MaxCasRetries = 100;

    /// <summary>
    /// Minimum report capacity for snapshot list.
    /// </summary>
    private const System.Int32 MinReportCapacity = 128;

    /// <summary>
    /// Maximum report capacity to prevent excessive allocation.
    /// </summary>
    private const System.Int32 MaxReportCapacity = 4096;

    #endregion Constants

    #region Fields

    private readonly System.Int32 _maxPerEndpoint;
    private readonly ConnectionLimitOptions _config;
    private readonly System.TimeSpan _cleanupInterval;
    private readonly System.TimeSpan _inactivityThreshold;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<INetworkEndpoint, ConnectionLimitInfo> _map;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private readonly ILogger _logger;

    private System.Int32 _disposed;

    // Metrics for monitoring
    private System.Int64 _totalConnectionAttempts;
    private System.Int64 _totalRejections;
    private System.Int64 _totalCleanedEntries;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="ConnectionLimiter"/> with optional configuration.
    /// </summary>
    /// <param name="config">Configuration options.  If null, uses global configuration.</param>
    /// <exception cref="InternalErrorException">Thrown when configuration validation fails.</exception>
    public ConnectionLimiter([System.Diagnostics.CodeAnalysis.AllowNull] ConnectionLimitOptions config = null)
    {
        _config = config ?? ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
        VALIDATE_CONFIGURATION(_config);

        _maxPerEndpoint = _config.MaxConnectionsPerIpAddress;
        _cleanupInterval = _config.CleanupInterval;
        _inactivityThreshold = _config.InactivityThreshold;

        _map = new System.Collections.Concurrent.ConcurrentDictionary<INetworkEndpoint, ConnectionLimitInfo>();
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        INITIALIZE_METRICS();
        SCHEDULE_CLEANUP_JOB();

        _logger?.Debug($"[NW.{nameof(ConnectionLimiter)}] init " +
                       $"maxPerEndpoint={_maxPerEndpoint} " +
                       $"inactivity={_inactivityThreshold.TotalSeconds:F0}s " +
                       $"cleanup={_cleanupInterval.TotalSeconds:F0}s");
    }

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
    public System.Boolean IsConnectionAllowed(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPEndPoint endPoint)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) != 0, nameof(ConnectionLimiter));
        VALIDATE_ENDPOINT(endPoint);

        _ = System.Threading.Interlocked.Increment(ref _totalConnectionAttempts);

        INetworkEndpoint key = CONVERT_TO_NETWORK_ENDPOINT(endPoint);
        System.DateTime now = System.DateTime.UtcNow;

        ConnectionAllowResult result = TRY_ACQUIRE_CONNECTION_SLOT(key, now);

        if (!result.Allowed)
        {
            _ = System.Threading.Interlocked.Increment(ref _totalRejections);
            _logger?.Trace($"[NW.{nameof(ConnectionLimiter)}] reject endpoint={endPoint} current={result.CurrentConnections} limit={_maxPerEndpoint}");
        }
        else
        {
            _logger?.Trace($"[NW.{nameof(ConnectionLimiter)}] allow endpoint={endPoint} current={result.CurrentConnections} limit={_maxPerEndpoint}");
        }

        return result.Allowed;
    }

    /// <summary>
    /// Attempts to acquire a connection slot for the given endpoint.
    /// </summary>
    /// <param name="endPoint">The endpoint requesting connection.</param>
    /// <returns>True if connection is allowed; false if limit exceeded.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if limiter is disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown if endPoint is null or invalid type.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsConnectionAllowed(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.EndPoint endPoint)
    {
        VALIDATE_ENDPOINT_TYPE(endPoint);
        return IsConnectionAllowed((System.Net.IPEndPoint)endPoint);
    }

    /// <summary>
    /// Handles connection closure event and decrements the connection counter.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="args">Connection event arguments.</param>
    /// <exception cref="System.ObjectDisposedException">Thrown if limiter is disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown if arguments are invalid.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    public void OnConnectionClosed(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs args)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) != 0, nameof(ConnectionLimiter));
        VALIDATE_CONNECTION_EVENT_ARGS(args);

        System.DateTime now = System.DateTime.UtcNow;
        INetworkEndpoint key = args.Connection.EndPoint;

        System.Boolean released = TRY_RELEASE_CONNECTION_SLOT(key, now);

        if (released)
        {
            _logger?.Trace($"[NW.{nameof(ConnectionLimiter)}] closed endpoint={key.Address}");
        }
    }

    /// <summary>
    /// Generates a human-readable diagnostic report of connection limiter state.
    /// </summary>
    /// <returns>Formatted report string.</returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public System.String GenerateReport()
    {
        System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<
                INetworkEndpoint, ConnectionLimitInfo>> snapshot = COLLECTS_NAPSHOT();

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

    #region Validation

    /// <summary>
    /// Validates configuration options.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_CONFIGURATION(ConnectionLimitOptions config)
    {
        if (config.MaxConnectionsPerIpAddress <= 0)
        {
            throw new InternalErrorException(
                $"{nameof(ConnectionLimitOptions.MaxConnectionsPerIpAddress)} must be > 0, got {config.MaxConnectionsPerIpAddress}");
        }

        if (config.InactivityThreshold <= System.TimeSpan.Zero)
        {
            throw new InternalErrorException(
                $"{nameof(ConnectionLimitOptions.InactivityThreshold)} must be > TimeSpan.Zero, got {config.InactivityThreshold}");
        }

        if (config.CleanupInterval <= System.TimeSpan.Zero)
        {
            throw new InternalErrorException(
                $"{nameof(ConnectionLimitOptions.CleanupInterval)} must be > TimeSpan.Zero, got {config.CleanupInterval}");
        }
    }

    /// <summary>
    /// Validates IP endpoint is not null.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_ENDPOINT(System.Net.IPEndPoint endPoint)
    {
        if (endPoint is null)
        {
            throw new InternalErrorException(
                $"[{nameof(ConnectionLimiter)}] EndPoint cannot be null",
                nameof(endPoint));
        }
    }

    /// <summary>
    /// Validates endpoint type is IPEndPoint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_ENDPOINT_TYPE(System.Net.EndPoint endPoint)
    {
        if (endPoint is null)
        {
            throw new InternalErrorException(
                $"[{nameof(ConnectionLimiter)}] EndPoint cannot be null",
                nameof(endPoint));
        }

        if (endPoint is not System.Net.IPEndPoint)
        {
            throw new InternalErrorException(
                $"[{nameof(ConnectionLimiter)}] EndPoint must be IPEndPoint, got {endPoint.GetType().Name}",
                nameof(endPoint));
        }
    }

    /// <summary>
    /// Validates connection event arguments.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_CONNECTION_EVENT_ARGS(IConnectEventArgs args)
    {
        if (args is null)
        {
            throw new InternalErrorException(
                $"[{nameof(ConnectionLimiter)}] Connection event args cannot be null",
                nameof(args));
        }

        if (args.Connection is null)
        {
            throw new InternalErrorException(
                $"[{nameof(ConnectionLimiter)}] Connection cannot be null",
                nameof(args));
        }

        if (args.Connection.EndPoint is null)
        {
            throw new InternalErrorException(
                $"[{nameof(ConnectionLimiter)}] Connection endpoint cannot be null",
                nameof(args));
        }
    }

    #endregion Validation

    #region Connection Slot Management

    /// <summary>
    /// Result of connection slot acquisition attempt.
    /// </summary>
    private readonly struct ConnectionAllowResult
    {
        public System.Boolean Allowed { get; init; }
        public System.Int32 CurrentConnections { get; init; }
    }

    /// <summary>
    /// Converts IPEndPoint to INetworkEndpoint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "<Pending>")]
    private static INetworkEndpoint CONVERT_TO_NETWORK_ENDPOINT(System.Net.IPEndPoint endPoint) => Connections.Connection.NetworkEndpoint.FromEndPoint(endPoint);

    /// <summary>
    /// Attempts to acquire a connection slot using lock-free CAS with bounded retries.
    /// </summary>
    private ConnectionAllowResult TRY_ACQUIRE_CONNECTION_SLOT(INetworkEndpoint key, System.DateTime now)
    {
        System.DateTime today = now.Date;

        // Ensure entry exists
        _ = _map.TryAdd(key, CREATE_INITIAL_CONNECTION(now));

        // ✅ FIX: Bounded CAS loop to prevent infinite retry
        for (System.Int32 attempt = 0; attempt < MaxCasRetries; attempt++)
        {
            if (!_map.TryGetValue(key, out ConnectionLimitInfo existing))
            {
                // Entry was removed - try recreate
                if (_map.TryAdd(key, CREATE_INITIAL_CONNECTION(now)))
                {
                    return new ConnectionAllowResult
                    {
                        Allowed = true,
                        CurrentConnections = 1
                    };
                }

                continue;
            }

            // Check limit
            if (existing.CurrentConnections >= _maxPerEndpoint)
            {
                return new ConnectionAllowResult
                {
                    Allowed = false,
                    CurrentConnections = existing.CurrentConnections
                };
            }

            // Calculate new totals (reset if new day)
            System.Int32 newTotalToday = CALCULATE_TOTAL_CONNECTIONS_TODAY(existing, today);

            // Propose update
            ConnectionLimitInfo proposed = existing with
            {
                CurrentConnections = existing.CurrentConnections + 1,
                LastConnectionTime = now,
                TotalConnectionsToday = newTotalToday
            };

            // Try atomic update
            if (_map.TryUpdate(key, proposed, existing))
            {
                return new ConnectionAllowResult
                {
                    Allowed = true,
                    CurrentConnections = proposed.CurrentConnections
                };
            }

            // CAS failed - retry with backoff
            if (attempt > 10)
            {
                System.Threading.Thread.SpinWait(1 << System.Math.Min(attempt - 10, 10));
            }
        }

        // ✅ FIX: Exhausted retries - fail safe by rejecting
        _logger?.Warn($"[NW. {nameof(ConnectionLimiter)}] CAS retry exhausted for {key.Address}");

        return new ConnectionAllowResult
        {
            Allowed = false,
            CurrentConnections = _maxPerEndpoint
        };
    }

    /// <summary>
    /// Creates initial connection info for new endpoint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ConnectionLimitInfo CREATE_INITIAL_CONNECTION(System.DateTime now)
    {
        return new ConnectionLimitInfo(
            currentConnections: 1,
            lastConnectionTime: now,
            totalConnectionsToday: 1);
    }

    /// <summary>
    /// Calculates total connections today, resetting if new day.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 CALCULATE_TOTAL_CONNECTIONS_TODAY(ConnectionLimitInfo existing, System.DateTime today)
        => today > existing.LastConnectionTime.Date ? 1 : existing.TotalConnectionsToday + 1;

    /// <summary>
    /// Attempts to release a connection slot using lock-free CAS.
    /// </summary>
    private System.Boolean TRY_RELEASE_CONNECTION_SLOT(INetworkEndpoint key, System.DateTime now)
    {
        if (!_map.TryGetValue(key, out _))
        {
            return false;
        }

        // ✅ Bounded retry for release too
        for (System.Int32 attempt = 0; attempt < MaxCasRetries; attempt++)
        {
            if (!_map.TryGetValue(key, out ConnectionLimitInfo existing))
            {
                return false;
            }

            ConnectionLimitInfo proposed = existing with
            {
                CurrentConnections = System.Math.Max(0, existing.CurrentConnections - 1),
                LastConnectionTime = now
            };

            if (_map.TryUpdate(key, proposed, existing))
            {
                return true;
            }

            // Backoff
            if (attempt > 10)
            {
                System.Threading.Thread.SpinWait(1 << System.Math.Min(attempt - 10, 10));
            }
        }

        _logger?.Warn($"[NW.{nameof(ConnectionLimiter)}] CAS retry exhausted during release for {key.Address}");
        return false;
    }

    #endregion Connection Slot Management

    #region Report Generation

    private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> COLLECTS_NAPSHOT()
    {
        System.Int32 estimatedCapacity = System.Math.Max(MinReportCapacity, System.Math.Min(_map.Count, MaxReportCapacity));

        ListPool<System.Collections.Generic.KeyValuePair<
            INetworkEndpoint, ConnectionLimitInfo>> pool = ListPool<System.Collections.Generic.KeyValuePair<
            INetworkEndpoint, ConnectionLimitInfo>>.Instance;

        System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<
                INetworkEndpoint, ConnectionLimitInfo>> snapshot = pool.Rent(minimumCapacity: estimatedCapacity);

        snapshot.AddRange(_map);

        return snapshot;
    }

    private System.String BUILD_REPORT(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        GlobalMetrics metrics = CALCULATE_GLOBAL_METRICS(snapshot);

        System.Text.StringBuilder sb = new();

        APPEND_REPORT_HEADER(sb, metrics);
        APPEND_CONNECTION_DETAILS(sb, snapshot);

        return sb.ToString();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void SORT_SNAPSHOT_BY_LOAD(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        snapshot.Sort(static (a, b) =>
        {
            System.Int32 byCurrent = b.Value.CurrentConnections.CompareTo(a.Value.CurrentConnections);
            return byCurrent != 0 ? byCurrent : b.Value.TotalConnectionsToday.CompareTo(a.Value.TotalConnectionsToday);
        });
    }

    private readonly struct GlobalMetrics
    {
        public System.Int32 TotalEndpoints { get; init; }
        public System.Int32 TotalConcurrent { get; init; }
        public System.Int64 TotalAttempts { get; init; }
        public System.Int64 TotalRejections { get; init; }
        public System.Int64 TotalCleaned { get; init; }
    }

    private GlobalMetrics CALCULATE_GLOBAL_METRICS(
        System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        System.Int32 totalConcurrent = 0;

        foreach (var kvp in snapshot)
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

    private void APPEND_REPORT_HEADER(
        System.Text.StringBuilder sb,
        GlobalMetrics metrics)
    {
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConnectionLimiter Status:");
        _ = sb.AppendLine($"MaxPerEndpoint     : {_maxPerEndpoint}");
        _ = sb.AppendLine($"CleanupInterval    : {_cleanupInterval.TotalSeconds:F0}s");
        _ = sb.AppendLine($"InactivityThreshold:  {_inactivityThreshold.TotalSeconds:F0}s");
        _ = sb.AppendLine($"TrackedEndpoints   : {metrics.TotalEndpoints}");
        _ = sb.AppendLine($"TotalConcurrent    : {metrics.TotalConcurrent}");
        _ = sb.AppendLine($"TotalAttempts      : {metrics.TotalAttempts: N0}");
        _ = sb.AppendLine($"TotalRejections    : {metrics.TotalRejections:N0}");
        _ = sb.AppendLine($"TotalCleaned       : {metrics.TotalCleaned:N0}");

        if (metrics.TotalAttempts > 0)
        {
            System.Double rejectionRate = metrics.TotalRejections * 100.0 / metrics.TotalAttempts;
            _ = sb.AppendLine($"RejectionRate      : {rejectionRate:F2}%");
        }

        _ = sb.AppendLine();
    }

    private static void APPEND_CONNECTION_DETAILS(
        System.Text.StringBuilder sb,
        System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        _ = sb.AppendLine("Top Endpoints by CurrentConnections:");
        _ = sb.AppendLine("---------------------------------------------------------------");
        _ = sb.AppendLine("Endpoint                   | Current | Today     | LastUtc");
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

    /// <summary>
    /// Appends top N endpoints to report.
    /// </summary>
    private static void APPEND_TOP_ENDPOINTS(
        System.Text.StringBuilder sb,
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot, System.Int32 maxRows)
    {
        System.Int32 rows = 0;

        foreach (var kvp in snapshot)
        {
            if (rows++ >= maxRows)
            {
                break;
            }

            System.String address = kvp.Key.Address ?? "unknown";
            ConnectionLimitInfo info = kvp.Value;

            // Format address column (truncate if too long)
            System.String addressCol = address.Length > 27
                ? $"{address[..27]}…"
                : address.PadRight(27);

            _ = sb.AppendLine(
                $"{addressCol} | {info.CurrentConnections,7} | {info.TotalConnectionsToday,9} | {info.LastConnectionTime:u}");
        }
    }

    /// <summary>
    /// Returns snapshot list to pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void RETURN_SNAPSHOT_TO_POOL(
        System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        ListPool<System.Collections.Generic.KeyValuePair<
            INetworkEndpoint, ConnectionLimitInfo>> pool = ListPool<System.Collections.Generic.KeyValuePair<
            INetworkEndpoint, ConnectionLimitInfo>>.Instance;

        pool.Return(snapshot, clearItems: true);
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
            System.DateTime nowUtc = System.DateTime.UtcNow;
            System.DateTime cutoff = nowUtc - _inactivityThreshold;

            System.Int32 scanned = 0;
            System.Int32 removed = 0;

            foreach (var kvp in _map)
            {
                if (scanned++ >= MaxCleanupKeysPerRun)
                {
                    break;
                }

                if (SHOULD_REMOVE_ENTRY(kvp.Value, cutoff))
                {
                    if (_map.TryRemove(kvp.Key, out _))
                    {
                        removed++;
                        _ = System.Threading.Interlocked.Increment(ref _totalCleanedEntries);
                    }
                }
            }

            if (removed > 0)
            {
                _logger?.Debug($"[NW.{nameof(ConnectionLimiter)}:Internal] cleanup scanned={scanned} removed={removed}");
            }
        }
        catch (System.Exception ex) when (ex is not System.ObjectDisposedException)
        {
            _logger?.Error($"[NW.{nameof(ConnectionLimiter)}:Internal] cleanup-error msg={ex.Message}");
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean SHOULD_REMOVE_ENTRY(ConnectionLimitInfo info, System.DateTime cutoff) => info.CurrentConnections <= 0 && info.LastConnectionTime < cutoff;

    #endregion Cleanup

    #region Initialization & Logging

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
            name: TaskNaming.Recurring.CleanupJobId(nameof(ConnectionLimiter), this.GetHashCode()),
            interval: _cleanupInterval,
            work: _ =>
            {
                this.RUN_CLEANUP_ONCE();
                return System.Threading.Tasks.ValueTask.CompletedTask;
            },
            options: new RecurringOptions
            {
                Tag = nameof(ConnectionLimiter),
                NonReentrant = true,
                Jitter = System.TimeSpan.FromMilliseconds(250),
                ExecutionTimeout = System.TimeSpan.FromSeconds(2),
                BackoffCap = System.TimeSpan.FromSeconds(15)
            }
        );
    }

    #endregion Initialization & Logging

    #region IDisposable & IAsyncDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        // Atomic check-and-set: 0 -> 1
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                        .CancelRecurring(TaskNaming.Recurring
                                        .CleanupJobId(nameof(ConnectionLimiter), this
                                        .GetHashCode()));

            _map.Clear();

            _logger?.Debug($"[NW. {nameof(ConnectionLimiter)}:{nameof(Dispose)}] disposed");
        }
        catch (System.Exception ex)
        {
            _logger?.Error($"[NW.{nameof(ConnectionLimiter)}:{nameof(Dispose)}] " +
                          $"dispose-error msg={ex.Message}");
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

    /// <summary>
    /// Stores connection tracking data for an endpoint.
    /// Immutable record struct for thread-safe CAS operations.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay(
        "Current:  {CurrentConnections}, Total: {TotalConnectionsToday}, Last: {LastConnectionTime}")]
    internal readonly record struct ConnectionLimitInfo
    {
        /// <summary>
        /// Current number of active connections.
        /// </summary>
        public System.Int32 CurrentConnections { get; init; }

        /// <summary>
        /// Timestamp of most recent connection.
        /// </summary>
        public System.DateTime LastConnectionTime { get; init; }

        /// <summary>
        /// Total connections established today (resets daily).
        /// </summary>
        public System.Int32 TotalConnectionsToday { get; init; }

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

    #endregion Internal Types
}