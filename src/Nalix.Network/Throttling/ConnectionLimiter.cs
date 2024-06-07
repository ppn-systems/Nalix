// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Connection;
using Nalix.Common.Exceptions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Name;
using Nalix.Framework.Tasks.Options;
using Nalix.Network.Configurations;
using Nalix.Network.Internal.Net;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Throttling;

/// <summary>
/// High-performance per-IP concurrent connection limiter.
/// Uses lock-free CAS updates on a ConcurrentDictionary to avoid lost updates under contention.
/// Supports cleanup of stale entries to bound memory usage.
/// </summary>
public sealed class ConnectionLimiter : System.IDisposable, IReportable
{
    #region Constants

    private const System.Int32 MaxCleanupKeysPerRun = 1000;

    #endregion Constants

    #region Fields

    private readonly System.Int32 _maxPerIp;
    private readonly ConnectionLimitOptions _config;
    private readonly System.TimeSpan _cleanupInterval;
    private readonly System.TimeSpan _inactivityThreshold;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IPAddressKey, ConnectionLimitInfo> _map;

    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="ConnectionLimiter"/> with an optional config. If <paramref name="config"/> is null, global config is used.
    /// </summary>
    public ConnectionLimiter(ConnectionLimitOptions? config)
    {
        _config = config ?? ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
        Validate(_config);

        _maxPerIp = _config.MaxConnectionsPerIpAddress;
        _cleanupInterval = _config.CleanupInterval;
        _inactivityThreshold = _config.InactivityThreshold;

        _map = new System.Collections.Concurrent.ConcurrentDictionary<IPAddressKey, ConnectionLimitInfo>();

        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: TaskNames.Recurring.WithKey(nameof(ConnectionLimiter), this.GetHashCode()),
            interval: _cleanupInterval,
            work: _ =>
            {
                RunCleanupOnce();
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

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(ConnectionLimiter)}] init maxPerIp={_maxPerIp} " +
                                       $"inactivity={_inactivityThreshold.TotalSeconds:F0}s " +
                                       $"cleanup={_cleanupInterval.TotalSeconds:F0}s");
    }

    /// <summary>
    /// Initializes a new <see cref="ConnectionLimiter"/> using configuration from <see cref="ConfigurationManager"/>.
    /// </summary>
    public ConnectionLimiter() : this(null)
    {
    }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Attempts to acquire a connection slot for the given IP address.
    /// Returns <c>true</c> if allowed (and increments counters), otherwise <c>false</c>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsConnectionAllowed(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPEndPoint endPoint)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (endPoint is null)
        {
            throw new InternalErrorException($"[{nameof(ConnectionLimiter)}] EndPoint cannot be null", nameof(endPoint));
        }

        IPAddressKey key = IPAddressKey.FromEndPoint(endPoint);

        System.DateTime now = System.DateTime.UtcNow;
        System.DateTime today = now.Date;

        // Ensure an entry exists
        _ = _map.TryAdd(key, new ConnectionLimitInfo(0, now, 0));

        // CAS loop: read → compute → TryUpdate until success
        while (true)
        {
            if (!_map.TryGetValue(key, out ConnectionLimitInfo existing))
            {
                if (_maxPerIp <= 0)
                {
                    return false;
                }

                ConnectionLimitInfo created = new(1, now, 1);
                if (_map.TryAdd(key, created))
                {
                    return true;
                }

                continue;
            }

            if (existing.CurrentConnections >= _maxPerIp)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(ConnectionLimiter)}] over-limit ip={endPoint} " +
                                               $"now={existing.CurrentConnections} limit={_maxPerIp}");
                return false;
            }

            System.Int32 totalToday = (today > existing.LastConnectionTime.Date)
                ? 1
                : existing.TotalConnectionsToday + 1;

            ConnectionLimitInfo proposed = existing with
            {
                CurrentConnections = existing.CurrentConnections + 1,
                LastConnectionTime = now,
                TotalConnectionsToday = totalToday
            };

            if (_map.TryUpdate(key, proposed, existing))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(ConnectionLimiter)}] allow ip={endPoint} " +
                                               $"now={proposed.CurrentConnections} limit={_maxPerIp}");
                return true;
            }
        }
    }

    /// <summary>
    /// Attempts to acquire a connection slot for the given IP address.
    /// Returns <c>true</c> if allowed (and increments counters), otherwise <c>false</c>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsConnectionAllowed(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Net.EndPoint? endPoint)
    {
        if (endPoint is null)
        {
            throw new InternalErrorException($"[{nameof(ConnectionLimiter)}] EndPoint cannot be null", nameof(endPoint));
        }
        if (endPoint is not System.Net.IPEndPoint ipEndPoint)
        {
            throw new InternalErrorException($"[{nameof(ConnectionLimiter)}] EndPoint must be IPEndPoint", nameof(endPoint));
        }

        return IsConnectionAllowed(ipEndPoint);
    }

    /// <summary>
    /// Marks a connection as closed for the given IP address. Returns <c>true</c> if the counter was decremented.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void OnConnectionClosed(System.Object? sender, IConnectEventArgs args)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (sender is null)
        {
            throw new InternalErrorException($"[{nameof(ConnectionLimiter)}] sender cannot be null", nameof(sender));
        }

        if (args.Connection.RemoteEndPoint is not System.Net.IPEndPoint endPoint)
        {
            return;
        }

        IPAddressKey key = IPAddressKey.FromEndPoint(endPoint);

        System.DateTime now = System.DateTime.UtcNow;

        if (!_map.TryGetValue(key, out _))
        {
            return;
        }

        while (true)
        {
            if (!_map.TryGetValue(key, out ConnectionLimitInfo existing))
            {
                return;
            }

            ConnectionLimitInfo proposed = existing with
            {
                CurrentConnections = System.Math.Max(0, existing.CurrentConnections - 1),
                LastConnectionTime = now
            };

            if (_map.TryUpdate(key, proposed, existing))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(ConnectionLimiter)}] close ip={endPoint} " +
                                               $"now={proposed.CurrentConnections} limit={_maxPerIp}");
                return;
            }
        }
    }

    /// <summary>
    /// Generates a human-readable diagnostic report for the current limiter state.
    /// The report includes config overview, global counters, and top IPs by load.
    /// </summary>
    /// <remarks>
    /// This method is allocation-friendly: uses a single StringBuilder, limits sorting,
    /// and caps the number of displayed rows for readability.
    /// </remarks>
    public System.String GenerateReport()
    {
        // Take a stable snapshot to minimize contention and keep the report consistent.
        // Copy to a local list once to avoid enumerating the concurrent map multiple times.
        System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<IPAddressKey, ConnectionLimitInfo>> snapshot = [.. _map];

        // Sort by current connections (desc), then by TotalToday (desc)
        snapshot.Sort(static (a, b) =>
        {
            System.Int32 byCurrent = b.Value.CurrentConnections.CompareTo(a.Value.CurrentConnections);
            return byCurrent != 0 ? byCurrent : b.Value.TotalConnectionsToday.CompareTo(a.Value.TotalConnectionsToday);
        });

        // Global totals
        System.Int32 totalIps = snapshot.Count;
        System.Int32 totalConcurrent = 0;
        foreach (System.Collections.Generic.KeyValuePair<IPAddressKey, ConnectionLimitInfo> kv in snapshot)
        {
            totalConcurrent += kv.Value.CurrentConnections;
        }

        // Build report
        System.Text.StringBuilder sb = new();
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConnectionLimiter Status:");
        _ = sb.AppendLine($"MaxPerIp           : {_maxPerIp}");
        _ = sb.AppendLine($"CleanupInterval    : {_cleanupInterval.TotalSeconds:F0}s");
        _ = sb.AppendLine($"TrackedIPs         : {totalIps}");
        _ = sb.AppendLine($"TotalConcurrent    : {totalConcurrent}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Top IPs by CurrentConnections:");
        _ = sb.AppendLine("---------------------------------------------------------------");
        _ = sb.AppendLine("IP Address                 | Current | Today     | LastUtc");
        _ = sb.AppendLine("---------------------------------------------------------------");

        System.Int32 rows = 0;
        foreach (var kv in snapshot)
        {
            if (rows++ >= 100)
            {
                break;
            }

            System.String ip = kv.Key.ToString() ?? "0.0.0.0";
            ConnectionLimitInfo info = kv.Value;

            // Pad/truncate IP for neat column alignment (IPv6 can be long).
            System.String ipCol = ip.Length > 27 ? $"{System.MemoryExtensions.AsSpan(ip, 0, 27)}…" : ip.PadRight(27);

            _ = sb.AppendLine($"{ipCol} | {info.CurrentConnections,7} | {info.TotalConnectionsToday,9} | {info.LastConnectionTime:u}");
        }

        if (rows == 0)
        {
            _ = sb.AppendLine("(no tracked IPs)");
        }

        _ = sb.AppendLine("---------------------------------------------------------------");
        return sb.ToString();
    }

    #endregion Public API

    #region Cleanup

    /// <summary>
    /// Timer tick entry: non-reentrant, best-effort cleanup of stale entries.
    /// </summary>
    private void RunCleanupOnce()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            System.DateTime nowUtc = System.DateTime.UtcNow;
            System.DateTime cutoff = nowUtc - _inactivityThreshold;

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
                    .Debug($"[{nameof(ConnectionLimiter)}] cleanup scanned={scanned} removed={removed}");
            }
        }
        catch (System.Exception ex) when (ex is not System.ObjectDisposedException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[{nameof(ConnectionLimiter)}] cleanup-error msg={ex.Message}");
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
            _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                        .CancelRecurring(TaskNames.Recurring.WithKey(nameof(ConnectionLimiter), this.GetHashCode()));
            _map.Clear();
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(ConnectionLimiter)}] dispose-error msg={ex.Message}");
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    #region Helpers

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Validate(ConnectionLimitOptions opt)
    {
        if (opt.MaxConnectionsPerIpAddress <= 0)
        {
            throw new InternalErrorException($"{nameof(ConnectionLimitOptions.MaxConnectionsPerIpAddress)} must be > 0");
        }

        if (opt.InactivityThreshold <= System.TimeSpan.Zero)
        {
            throw new InternalErrorException($"{nameof(ConnectionLimitOptions.InactivityThreshold)} must be > 0");
        }

        if (opt.CleanupInterval <= System.TimeSpan.Zero)
        {
            throw new InternalErrorException($"{nameof(ConnectionLimitOptions.CleanupInterval)} must be > 0");
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
        /// The current ProtocolType of active connections.
        /// </summary>
        public readonly System.Int32 CurrentConnections { get; init; }

        /// <summary>
        /// When the most recent connection was established.
        /// </summary>
        public readonly System.DateTime LastConnectionTime { get; init; }

        /// <summary>
        /// The total ProtocolType of connections made today.
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
