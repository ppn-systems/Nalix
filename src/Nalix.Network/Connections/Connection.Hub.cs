// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Security;
using Nalix.Environment.Configuration;
using Nalix.Environment.Time;
using Nalix.Framework.Extensions;
using Nalix.Network.Options;
using Nalix.Network.Sessions;

namespace Nalix.Network.Connections;

/// <summary>
/// Manages connections for servers, optimized for high performance and thread safety.
/// </summary>
/// <remarks>
/// This class provides efficient connection management with minimal allocations and fast lookup operations.
/// It is thread-safe and uses concurrent collections to handle multiple connections simultaneously.
/// <para>
/// Session persistence is delegated to an <see cref="ISessionStore"/>. By default an
/// <see cref="InMemorySessionStore"/> is used; inject a custom implementation for distributed scenarios.
/// </para>
/// </remarks>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("ConnectionHub (Count={_count})")]
public sealed class ConnectionHub : IConnectionHub
{
    #region Fields

    /// <summary>
    /// Queue tracking order of anonymous connections for O(1)-amortized eviction
    /// </summary>
    private readonly ConcurrentQueue<ulong> _anonymousQueue;

    private readonly int _shardMask;
    private readonly int _shardCount;
    private readonly int _maxConnections;
    private readonly bool _trackEvictionQueue;
    private readonly bool _isPowerOfTwoShardCount;
    private readonly ConcurrentDictionary<ulong, IConnection>[] _shards;

    private readonly ISessionStore _sessionStore;
    private readonly SessionStoreOptions _sessionOptions;

    private readonly ILogger? _logger;
    private readonly ConnectionHubOptions _options;

    /// <summary>
    /// Connections statistics for monitoring
    /// </summary>
    private int _count;
    private volatile bool _disposed;
    private int _evictedConnections;
    private int _rejectedConnections;

    /// <summary>
    /// Outbound-allocated collections for bulk operations
    /// </summary>
    private static readonly System.Buffers.ArrayPool<IConnection> s_connectionPool;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current number of active connections.
    /// </summary>
    public int Count => Volatile.Read(ref _count);

    /// <inheritdoc />
    public ISessionStore SessionStore => _sessionStore;

    /// <summary>
    /// Raised after a connection is successfully unregistered.
    /// </summary>
    public event Action<IConnection>? ConnectionUnregistered;

    /// <summary>
    /// Raised when a limit is reached (e.g., max connections) and a connection is rejected.
    /// </summary>
    public event CapacityLimitReachedHandler? CapacityLimitReached;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes static members of the <see cref="ConnectionHub"/> class.
    /// </summary>
    static ConnectionHub() => s_connectionPool = System.Buffers.ArrayPool<IConnection>.Shared;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionHub"/> class.
    /// </summary>
    /// <param name="sessionStore">
    /// The session store used to persist connection sessions.
    /// Defaults to <see cref="InMemorySessionStore"/> when <c>null</c>.
    /// </param>
    /// <param name="logger">The logger instance to use for logging.</param>
    public ConnectionHub(ISessionStore? sessionStore = null, ILogger? logger = null)
    {
        _options = ConfigurationManager.Instance.Get<ConnectionHubOptions>();
        _options.Validate();

        _logger = logger;
        _sessionStore = sessionStore ?? new InMemorySessionStore();
        _sessionOptions = ConfigurationManager.Instance.Get<SessionStoreOptions>();

        /*
         * [Sharding Logic]
         * We shard the connections across multiple ConcurrentDictionaries.
         * This significantly reduces contention on the internal dictionary 
         * locks when many connections are being registered/unregistered 
         * simultaneously on a high-core machine.
         */
        _maxConnections = _options.MaxConnections;
        _shardCount = Math.Max(1, _options.ShardCount);
        _isPowerOfTwoShardCount = (_shardCount & (_shardCount - 1)) == 0;
        _shardMask = _shardCount - 1;
        _trackEvictionQueue = _maxConnections > 0 && _options.DropPolicy == DropPolicy.DropOldest;
        _anonymousQueue = new ConcurrentQueue<ulong>();
        _shards = new ConcurrentDictionary<ulong, IConnection>[_shardCount];

        int perShardCapacity = _maxConnections > 0
            ? Math.Max(4, (_maxConnections + _shardCount - 1) / _shardCount)
            : 31;

        for (int i = 0; i < _shardCount; i++)
        {
            _shards[i] = new ConcurrentDictionary<ulong, IConnection>(
                concurrencyLevel: System.Environment.ProcessorCount,
                capacity: perShardCapacity);
        }
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc />
    /// <summary>
    /// Registers a new connection with the hub.
    /// </summary>
    /// <param name="connection">The connection to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="connection"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void RegisterConnection(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        RegisterResult result = this.TryRegisterCore(connection);
        if (result == RegisterResult.Success)
        {
            return;
        }

        ObjectDisposedException.ThrowIf(result == RegisterResult.Disposed, nameof(ConnectionHub));

        if (result == RegisterResult.Duplicate)
        {
            throw new InternalErrorException($"Connection '{connection.ID}' is already registered.");
        }

        throw new InternalErrorException(
            $"Connection hub capacity reached. Maximum connections: {_maxConnections}.");
    }

    /// <inheritdoc />
    /// <summary>
    /// Unregisters a connection from the hub.
    /// </summary>
    /// <param name="connection">The connection to unregister.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void UnregisterConnection(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _ = this.TryUnregisterCore(connection);
    }

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a connection by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the connection to retrieve.</param>
    /// <returns>The connection associated with the identifier, or <c>null</c> if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: MaybeNull]
    public IConnection? GetConnection(ISnowflake id)
    {
        ArgumentNullException.ThrowIfNull(id);

        ulong key = id.ToUInt64();
        ConcurrentDictionary<ulong, IConnection> shard = this.GetShard(key);
        return shard.TryGetValue(key, out IConnection? connection) ? connection : null;
    }

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a connection by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the connection to retrieve.</param>
    /// <returns>The connection associated with the identifier, or <c>null</c> if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: MaybeNull]
    public IConnection? GetConnection(ulong id)
    {
        ConcurrentDictionary<ulong, IConnection> shard = this.GetShard(id);
        return shard.TryGetValue(id, out IConnection? connection) ? connection : null;
    }

    /// <summary>
    /// Retrieves a connection by its serialized identifier.
    /// </summary>
    /// <param name="id">The serialized identifier of the connection.</param>
    /// <returns>The connection associated with the identifier, or <c>null</c> if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: MaybeNull]
    public IConnection? GetConnection(ReadOnlySpan<byte> id)
    {
        ulong key = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(id);
        ConcurrentDictionary<ulong, IConnection> shard = this.GetShard(key);
        return shard.TryGetValue(key, out IConnection? connection) ? connection : null;
    }

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a read-only collection of all active connections.
    /// </summary>
    /// <returns>A read-only collection of active connections.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public IReadOnlyCollection<IConnection> ListConnections()
    {
        if (_disposed || Volatile.Read(ref _count) == 0)
        {
            return Array.Empty<IConnection>();
        }

        return this.CaptureConnectionSnapshot();
    }

    /// <summary>
    /// Broadcasts a message to all active connections.
    /// </summary>
    /// <typeparam name="T">The type of the message to broadcast.</typeparam>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="sendFunc">The function to send the message to a connection.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous broadcast operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="message"/> or <paramref name="sendFunc"/> is null.</exception>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "<Pending>")]
    public async Task BroadcastAsync<T>(
        T message,
        Func<IConnection, T, Task> sendFunc,
        CancellationToken cancellationToken = default) where T : class
    {
        if (message is null || sendFunc is null || _disposed)
        {
            return;
        }

        /*
         * [Broadcast Logic]
         * To broadcast a message, we first capture a stable snapshot of all 
         * active connections. This ensures that we don't hold the dictionary 
         * locks while performing I/O.
         */
        IConnection[] connections = this.CaptureConnectionSnapshot();
        if (connections.Length == 0)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastAsync)}] broadcast-skip total=0");
            }

            return;
        }

        ILogger? logger = _logger;
        bool measureLatency = _options.IsEnableLatency && logger?.IsEnabled(LogLevel.Information) == true;
        TimingScope scope = measureLatency ? TimingScope.Start() : default;

        if (_options.BroadcastBatchSize > 0)
        {
            await this.BroadcastBatchedAsync(connections, message, sendFunc, cancellationToken)
                      .ConfigureAwait(false);
        }
        else
        {
            await this.BroadcastCoreAsync(
                connections, message, sendFunc,
                predicate: null, cancellationToken,
                nameof(BroadcastAsync)).ConfigureAwait(false);
        }

        if (measureLatency && logger != null)
        {
            logger.LogInformation($"[PERF.NW.BroadcastAsync] total={connections.Length}, latency={scope.GetElapsedMilliseconds():F3} ms");
        }
    }

    /// <summary>
    /// Broadcasts a message to connections matching the given predicate.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public async Task BroadcastWhereAsync<T>(
        T message,
        Func<IConnection, T, Task> sendFunc,
        Func<IConnection, bool> predicate,
        CancellationToken cancellation = default) where T : class
    {
        if (message is null || sendFunc is null || _disposed)
        {
            return;
        }

        IConnection[] connections = this.CaptureConnectionSnapshot();
        if (connections.Length == 0)
        {
            return;
        }

        await this.BroadcastCoreAsync(
            connections, message, sendFunc,
            predicate, cancellation,
            nameof(BroadcastWhereAsync)).ConfigureAwait(false);
    }

    /// <summary>
    /// Forcibly closes all connections matching the specified IP address.
    /// </summary>
    /// <param name="networkEndpoint">The IP address to forcefully close.</param>
    /// <returns>Number of connections closed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="networkEndpoint"/> is null.</exception>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public int ForceClose(INetworkEndpoint networkEndpoint)
    {
        ArgumentNullException.ThrowIfNull(networkEndpoint);

        if (_disposed)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning($"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] called on disposed instance.");
            }
            return 0;
        }

        int closedCount = 0;
        string targetAddress = networkEndpoint.Address;

        foreach (ConcurrentDictionary<ulong, IConnection> shard in _shards)
        {
            foreach (IConnection conn in shard.Values)
            {
                if (conn.NetworkEndpoint.Address != targetAddress)
                {
                    continue;
                }

                try
                {
                    conn.Disconnect("Force disconnected by IP.");
                    closedCount++;
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    conn.ThrottledError(
                        _logger,
                        "hub.force_close_error",
                        $"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] disconnect failed id={conn.ID}", ex);
                }
            }
        }

        if (closedCount > 0)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    $"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] closed={closedCount} ip={targetAddress}");
            }
        }

        return closedCount;
    }

    /// <summary>
    /// Closes all active connections with an optional reason.
    /// </summary>
    /// <param name="reason">The reason for closing the connections, if any.</param>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public void CloseAllConnections(string? reason = null)
    {
        IConnection[] connections = this.CaptureConnectionSnapshot();

        foreach (IConnection connection in connections)
        {
            connection.OnCloseEvent -= this.OnClientDisconnected;
        }

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = _options.ParallelDisconnectDegree
        };

        _ = Parallel.ForEach(connections, parallelOptions, connection =>
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                connection.ThrottledError(
                    _logger,
                    "hub.close_all_error",
                    $"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-error id={connection.ID}",
                    ex);
            }
        });

        foreach (ConcurrentDictionary<ulong, IConnection> shard in _shards)
        {
            shard.Clear();
        }

        _anonymousQueue.Clear();
        _ = Interlocked.Exchange(ref _count, 0);

        if (_logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                $"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-all total={connections.Length}");
        }
    }

    /// <summary>
    /// Generates a human-readable report of active connections and statistics.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public string GenerateReport()
    {
        const int Limit = 15;

        int count = 0;
        long sumBytesSent = 0, sumUptime = 0, maxUptime = 0, minUptime = long.MaxValue;

        StringBuilder sb = new(1024);
        Dictionary<string, int> algoCounts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> statusCounts = new(StringComparer.OrdinalIgnoreCase);

        _ = sb.AppendLine(CultureInfo.InvariantCulture,
            $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConnectionHub Status:");
        int total = Volatile.Read(ref _count);
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Connections    : {total}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture,
            $"Evicted Connections  : {Volatile.Read(ref _evictedConnections)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture,
            $"Rejected Connections : {Volatile.Read(ref _rejectedConnections)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Shard Count          : {_shardCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture,
            $"Anonymous Queue Depth: {this.CountAnonymousConnections()}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture,
            $"Max Connections      : {(_maxConnections < 0 ? "Unlimited" : _maxConnections.ToString(CultureInfo.InvariantCulture))}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Drop Policy          : {_options.DropPolicy}");

        foreach (ConcurrentDictionary<ulong, IConnection> shard in _shards)
        {
            foreach (IConnection conn in shard.Values)
            {
                sumBytesSent += conn.BytesSent;

                long up = conn.UpTime;
                sumUptime += up;
                if (up > maxUptime)
                {
                    maxUptime = up;
                }

                if (up < minUptime)
                {
                    minUptime = up;
                }

                string status = conn.Level.ToString();
                string algo = conn.Algorithm.ToString();
                algoCounts[algo] = algoCounts.TryGetValue(algo, out int n) ? n + 1 : 1;
                statusCounts[status] = statusCounts.TryGetValue(status, out int cur) ? cur + 1 : 1;
            }
        }

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Bytes Sent   : {sumBytesSent:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture,
            $"Average Uptime     : {(total > 0 ? sumUptime / total : 0)}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Max Connection Time: {maxUptime}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture,
            $"Min Connection Time: {(minUptime == long.MaxValue ? 0 : minUptime)}s");

        _ = sb.AppendLine();
        _ = sb.AppendLine("Connection Status Summary:");
        _ = sb.AppendLine("----------------------------------------");
        _ = sb.AppendLine("Status          | Count");
        _ = sb.AppendLine("----------------------------------------");
        foreach (KeyValuePair<string, int> kvp in statusCounts)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{kvp.Key,-15} | {kvp.Value,5}");
        }

        _ = sb.AppendLine("----------------------------------------");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Algorithm Summary:");
        _ = sb.AppendLine("----------------------------------------");
        _ = sb.AppendLine("Algorithm         | Count");
        _ = sb.AppendLine("----------------------------------------");
        foreach (KeyValuePair<string, int> kvp in algoCounts)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{kvp.Key,-16} | {kvp.Value,5}");
        }

        _ = sb.AppendLine("----------------------------------------");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Active Connections:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine("ID             | Username");
        _ = sb.AppendLine("------------------------------------------------------------");

        foreach (ConcurrentDictionary<ulong, IConnection> shard in _shards)
        {
            foreach (KeyValuePair<ulong, IConnection> kvp in shard)
            {
                ulong id = kvp.Key;
                string username = kvp.Value.Attributes.TryGetValue("username", out object? name) && name is string s
                    ? s
                    : "N/A";
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{id,-14} | {username}");

                if (++count >= Limit)
                {
                    break;
                }
            }

            if (count >= Limit)
            {
                break;
            }
        }

        _ = sb.AppendLine("------------------------------------------------------------");
        return sb.ToString();
    }

    /// <summary>
    /// Generates a key-value diagnostic summary of the connection hub and active connections.
    /// </summary>
    public IDictionary<string, object> GetReportData()
    {
        int total = Volatile.Read(ref _count);
        Dictionary<string, object> report = new()
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["TotalConnections"] = total,
            ["EvictedConnections"] = Volatile.Read(ref _evictedConnections),
            ["RejectedConnections"] = Volatile.Read(ref _rejectedConnections),
            ["ShardCount"] = _shardCount,
            ["AnonymousQueueDepth"] = this.CountAnonymousConnections(),
            ["MaxConnections"] = _maxConnections,
            ["DropPolicy"] = _options.DropPolicy.ToString(),
        };

        long sumBytesSent = 0, sumUptime = 0, maxUptime = 0, minUptime = long.MaxValue;
        Dictionary<string, int> algoCounts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> statusCounts = new(StringComparer.OrdinalIgnoreCase);

        int limit = 15, current = 0;
        List<Dictionary<string, object>> sampleConnections = [];

        foreach (ConcurrentDictionary<ulong, IConnection> shard in _shards)
        {
            foreach (IConnection conn in shard.Values)
            {
                sumBytesSent += conn.BytesSent;
                long up = conn.UpTime;
                sumUptime += up;
                if (up > maxUptime)
                {
                    maxUptime = up;
                }

                if (up < minUptime)
                {
                    minUptime = up;
                }

                string status = conn.Level.ToString();
                string algo = conn.Algorithm.ToString();
                algoCounts[algo] = algoCounts.TryGetValue(algo, out int n) ? n + 1 : 1;
                statusCounts[status] = statusCounts.TryGetValue(status, out int cnt) ? cnt + 1 : 1;

                if (current++ < limit)
                {
                    string username = "N/A";
                    if (conn.Attributes.TryGetValue("username", out object? v) && v is string s)
                    {
                        username = s;
                    }

                    sampleConnections.Add(new Dictionary<string, object>
                    {
                        ["ID"] = conn.ID.ToString() ?? "N/A",
                        ["Username"] = username,
                        ["Level"] = status,
                        ["Algorithm"] = algo,
                        ["BytesSent"] = conn.BytesSent,
                        ["UpTime"] = conn.UpTime
                    });
                }
            }
        }

        report["TotalBytesSent"] = sumBytesSent;
        report["AverageUptimeSeconds"] = total > 0 ? (sumUptime / total) : 0;
        report["MaxConnectionTime"] = maxUptime;
        report["MinConnectionTime"] = minUptime == long.MaxValue ? 0 : minUptime;
        report["ConnectionStatusSummary"] = statusCounts;
        report["AlgorithmSummary"] = algoCounts;
        report["SampleConnections"] = sampleConnections;

        return report;
    }

    /// <inheritdoc />
    /// <summary>
    /// Releases all resources used by the <see cref="ConnectionHub"/> and closes all connections.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        this.CloseAllConnections("disposed");

        if (_sessionStore is IDisposable disposableStore)
        {
            disposableStore.Dispose();
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation($"[NW.{nameof(ConnectionHub)}:{nameof(Dispose)}] disposed");
        }
    }

    #endregion APIs

    #region Private Methods

    /*
     * [Shard Index Calculation]
     * If shard count is power-of-two, we use bitwise AND (extremely fast).
     * Otherwise, we fallback to modulo operator.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetShardIndex(ulong id)
    {
        int hash = id.GetHashCode() & int.MaxValue;
        return _isPowerOfTwoShardCount ? (hash & _shardMask) : (hash % _shardCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ConcurrentDictionary<ulong, IConnection> GetShard(ulong id)
    {
        int index = this.GetShardIndex(id);
        return _shards[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "<Pending>")]
    private RegisterResult TryRegisterCore(IConnection connection)
    {
        if (_disposed)
        {
            return RegisterResult.Disposed;
        }

        bool measureLatency = _options.IsEnableLatency && _logger?.IsEnabled(LogLevel.Information) == true;
        TimingScope scope = measureLatency ? TimingScope.Start() : default;

        ulong connectionKey = connection.ID.ToUInt64();
        if (!this.TryReserveCapacitySlot(connection, out RegisterResult failure))
        {
            return failure;
        }

        connection.OnCloseEvent += this.OnClientDisconnected;
        connection.Attributes[ConnectionAttributes.OwnerHub] = this;

        bool added = false;
        try
        {
            ConcurrentDictionary<ulong, IConnection> shard = this.GetShard(connectionKey);
            if (!shard.TryAdd(connectionKey, connection))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        $"[NW.{nameof(ConnectionHub)}:{nameof(RegisterConnection)}] register-dup id={connection.ID}");
                }

                return RegisterResult.Duplicate;
            }

            added = true;
            if (_trackEvictionQueue && this.IsAnonymousConnection(connection))
            {
                _anonymousQueue.Enqueue(connectionKey);
            }

            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    $"[NW.{nameof(ConnectionHub)}:{nameof(RegisterConnection)}] register id={connection.ID} total={Volatile.Read(ref _count)}");
            }

            if (measureLatency && _logger != null)
            {
                _logger.LogInformation(
                    $"[PERF.NW.RegisterConnection] id={connection.ID}, latency={scope.GetElapsedMilliseconds():F3} ms");
            }

            return RegisterResult.Success;
        }
        finally
        {
            if (!added)
            {
                connection.OnCloseEvent -= this.OnClientDisconnected;
                _ = Interlocked.Decrement(ref _count);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "<Pending>")]
    private bool TryUnregisterCore(IConnection connection)
    {
        ulong connectionKey = connection.ID.ToUInt64();
        ConcurrentDictionary<ulong, IConnection> shard = this.GetShard(connectionKey);

#pragma warning disable CA2000 // Dispose objects before losing scope
        if (!shard.TryRemove(connectionKey, out IConnection? existing))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    $"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister-miss id={connection.ID}");
            }

            return false;
        }
#pragma warning restore CA2000 // Dispose objects before losing scope

        bool measureLatency = _options.IsEnableLatency && _logger?.IsEnabled(LogLevel.Information) == true;
        TimingScope scope = measureLatency ? TimingScope.Start() : default;

        IConnection removedConnection = existing ?? connection;
        removedConnection.OnCloseEvent -= this.OnClientDisconnected;
        _ = Interlocked.Decrement(ref _count);

        if (_sessionOptions.AutoSaveOnUnregister)
        {
            _ = PersistBackgroundAsync(_sessionStore, removedConnection);
        }

        static async Task PersistBackgroundAsync(ISessionStore store, IConnection connection)
        {
            try
            {
                await store.StoreAsync(connection).ConfigureAwait(false);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // Background persistence failures (including policy violations) are ignored in fire-and-forget scenarios
            }
        }

        try
        {
            removedConnection.Dispose();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] dispose-error id={removedConnection.ID}");
            }
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                $"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister id={removedConnection.ID} total={Volatile.Read(ref _count)}");
        }

        ConnectionUnregistered?.Invoke(removedConnection);

        if (measureLatency && _logger != null)
        {
            _logger.LogInformation(
                $"[PERF.NW.UnregisterConnection] id={removedConnection.ID}, latency={scope.GetElapsedMilliseconds():F3} ms");
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool TryReserveCapacitySlot(IConnection incomingConnection, out RegisterResult failure)
    {
        failure = RegisterResult.Success;

        if (_maxConnections < 0)
        {
            _ = Interlocked.Increment(ref _count);
            return true;
        }

        /* 
         * ARCHITECTURAL DESIGN: LOCK-FREE CAPACITY RESERVATION
         * We use an optimistic reservation pattern here to avoid a global lock on the hub.
         * 1. Atomically increment the total connection count.
         * 2. If we are within limits, the slot is ours.
         * 3. If we exceed the limit, we decrement it back (rollback) and enter the more expensive
         *    eviction or blocking logic. This ensures that 99.9% of connection attempts (when 
         *    below capacity) complete without ever touching a Mutex or SpinLock.
         */
        int current = Interlocked.Increment(ref _count);
        if (current <= _maxConnections)
        {
            return true;
        }

        // Over capacity, need to revert the counter or attempt eviction of anonymous clients
        _ = Interlocked.Decrement(ref _count);

        if (_disposed)
        {
            failure = RegisterResult.Disposed;
            return false;
        }

        switch (_options.DropPolicy)
        {
            case DropPolicy.DropOldest:
                if (this.TryEvictOldestConnection(incomingConnection))
                {
                    // Recursively try again after eviction
                    return this.TryReserveCapacitySlot(incomingConnection, out failure);
                }

                this.RejectIncomingConnection(incomingConnection, "evict-oldest-no-candidate");
                failure = RegisterResult.CapacityLimitReached;
                return false;

            case DropPolicy.Block:
                /*
                 * ANTI-SPIN PROTECTION:
                 * While blocking is requested, we must not burn CPU indefinitely if the server
                 * is genuinely saturated. We use a SpinWait that starts with thread-yielding
                 * and eventually transitions to Sleep(0)/Sleep(1) to play nice with the OS scheduler.
                 */
                SpinWait spinner = default;
                while (true)
                {
                    spinner.SpinOnce();

                    // Prevent infinite spin. If we've waited too long, we treat it as a timeout
                    // to prevent a "deadlock" state where all acceptor threads are spinning.
                    if (spinner.Count > 10_000)
                    {
                        this.RejectIncomingConnection(incomingConnection, "block-timeout");
                        failure = RegisterResult.CapacityLimitReached;
                        return false;
                    }

                    // CAS loop to safely re-claim a slot if one becomes free
                    int c = Volatile.Read(ref _count);
                    if (c < _maxConnections)
                    {
                        if (Interlocked.CompareExchange(ref _count, c + 1, c) == c)
                        {
                            return true;
                        }
                    }
                }

            case DropPolicy.Coalesce:
            case DropPolicy.DropNewest:
            default:
                this.RejectIncomingConnection(incomingConnection, "drop-newest");
                failure = RegisterResult.CapacityLimitReached;
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool TryEvictOldestConnection(IConnection incomingConnection)
    {
        while (_anonymousQueue.TryDequeue(out ulong oldestId))
        {
            ConcurrentDictionary<ulong, IConnection> shard = this.GetShard(oldestId);
            if (!shard.TryGetValue(oldestId, out IConnection? candidate) || candidate is null)
            {
                continue;
            }

            if (!this.IsAnonymousConnection(candidate))
            {
                continue;
            }

#pragma warning disable CA2000 // Dispose objects before losing scope
            if (!shard.TryRemove(oldestId, out IConnection? evictedConnection) || evictedConnection is null)
            {
                continue;
            }
#pragma warning restore CA2000 // Dispose objects before losing scope

            evictedConnection.OnCloseEvent -= this.OnClientDisconnected;
            _ = Interlocked.Decrement(ref _count);
            _ = Interlocked.Increment(ref _evictedConnections);

            this.NotifyCapacityLimit(incomingConnection, "evict-oldest");
            ConnectionUnregistered?.Invoke(evictedConnection);

            if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    $"[NW.{nameof(ConnectionHub)}:{nameof(TryEvictOldestConnection)}] evicted id={evictedConnection.ID}");
            }

            try
            {
                evictedConnection.Disconnect("evicted to make room for new connection");
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex,
                        $"[NW.{nameof(ConnectionHub)}:{nameof(TryEvictOldestConnection)}] evict-disconnect-failed id={evictedConnection.ID}");
                }
            }

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsAnonymousConnection(IConnection connection) => connection.Level == PermissionLevel.NONE;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int CountAnonymousConnections()
    {
        int anonymous = 0;

        foreach (ConcurrentDictionary<ulong, IConnection> shard in _shards)
        {
            foreach (IConnection connection in shard.Values)
            {
                if (this.IsAnonymousConnection(connection))
                {
                    anonymous++;
                }
            }
        }

        return anonymous;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RejectIncomingConnection(IConnection incomingConnection, string reason)
    {
        _ = Interlocked.Increment(ref _rejectedConnections);
        this.NotifyCapacityLimit(incomingConnection, reason);

        try
        {
            incomingConnection.Disconnect("connection limit reached");
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex,
                    $"[NW.{nameof(ConnectionHub)}:{nameof(RejectIncomingConnection)}] reject-disconnect-failed id={incomingConnection.ID}");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private IConnection[] CaptureConnectionSnapshot()
    {
        int estimatedCount = Math.Max(4, Volatile.Read(ref _count));
        IConnection[] buffer = s_connectionPool.Rent(estimatedCount);

        try
        {
            int index = 0;
            foreach (ConcurrentDictionary<ulong, IConnection> shard in _shards)
            {
                foreach (KeyValuePair<ulong, IConnection> kvp in shard)
                {
                    if (index >= buffer.Length)
                    {
                        // Expand buffer if needed
                        IConnection[] newBuffer = s_connectionPool.Rent(buffer.Length * 2);
                        Array.Copy(buffer, newBuffer, buffer.Length);
                        s_connectionPool.Return(buffer);
                        buffer = newBuffer;
                    }
                    buffer[index++] = kvp.Value;
                }
            }

            if (index == 0)
            {
                return Array.Empty<IConnection>();
            }

            IConnection[] snapshot = new IConnection[index];
            Array.Copy(buffer, snapshot, index);
            return snapshot;
        }
        finally
        {
            s_connectionPool.Return(buffer, clearArray: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task BroadcastCoreAsync<T>(
        IConnection[] connections,
        T message,
        Func<IConnection, T, Task> sendFunc,
        Func<IConnection, bool>? predicate,
        CancellationToken cancellationToken,
        string operationName) where T : class
    {
        Task[] tasks = System.Buffers.ArrayPool<Task>.Shared.Rent(connections.Length);
        IConnection[] owners = s_connectionPool.Rent(connections.Length);
        int taskCount = 0;

        try
        {
            for (int i = 0; i < connections.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                IConnection connection = connections[i];
                if (predicate is not null && !predicate(connection))
                {
                    continue;
                }

                try
                {
                    Task sendTask = sendFunc(connection, message);
                    if (!sendTask.IsCompletedSuccessfully)
                    {
                        tasks[taskCount] = sendTask;
                        owners[taskCount] = connection;
                        taskCount++;
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex,
                            $"[NW.{nameof(ConnectionHub)}:{operationName}] send-failure id={connection.ID}");
                    }
                }
            }

            if (taskCount == 0)
            {
                return;
            }

            try
            {
                await Task.WhenAll(MemoryExtensions.AsSpan(tasks, 0, taskCount)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation($"[NW.{nameof(ConnectionHub)}:{operationName}] broadcast-cancel");
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                this.LogBroadcastFailures(tasks, owners, taskCount, operationName);
            }
        }
        finally
        {
            Array.Clear(tasks, 0, taskCount);
            Array.Clear(owners, 0, taskCount);
            System.Buffers.ArrayPool<Task>.Shared.Return(tasks, clearArray: true);
            s_connectionPool.Return(owners, clearArray: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LogBroadcastFailures(
        Task[] tasks,
        IConnection[] owners,
        int taskCount,
        string operationName)
    {
        for (int i = 0; i < taskCount; i++)
        {
            Task task = tasks[i];
            if (!task.IsFaulted)
            {
                continue;
            }

            Exception? exception = task.Exception?.GetBaseException();
            if (exception is null)
            {
                continue;
            }

            IConnection owner = owners[i];
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(exception,
                    $"[NW.{nameof(ConnectionHub)}:{operationName}] send-failure id={owner.ID}");
            }
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task BroadcastBatchedAsync<T>(
        IConnection[] connections,
        T message,
        Func<IConnection, T, Task> sendFunc,
        CancellationToken cancellationToken) where T : class
    {
        int batchSize = Math.Max(1, _options.BroadcastBatchSize);
        Task[] tasks = System.Buffers.ArrayPool<Task>.Shared.Rent(batchSize);
        IConnection[] owners = s_connectionPool.Rent(batchSize);
        int taskCount = 0;

        try
        {
            for (int i = 0; i < connections.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                IConnection connection = connections[i];

                try
                {
                    Task sendTask = sendFunc(connection, message);
                    if (!sendTask.IsCompletedSuccessfully)
                    {
                        tasks[taskCount] = sendTask;
                        owners[taskCount] = connection;
                        taskCount++;
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex,
                            $"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastBatchedAsync)}] send-failure id={connection.ID}");
                    }
                }

                if (taskCount < batchSize)
                {
                    continue;
                }

                await this.AwaitBatchAsync(tasks, owners, taskCount, cancellationToken,
                              nameof(BroadcastBatchedAsync)).ConfigureAwait(false);
                Array.Clear(tasks, 0, taskCount);
                Array.Clear(owners, 0, taskCount);
                taskCount = 0;
            }

            if (taskCount > 0)
            {
                await this.AwaitBatchAsync(tasks, owners, taskCount, cancellationToken,
                              nameof(BroadcastBatchedAsync)).ConfigureAwait(false);
            }
        }
        finally
        {
            Array.Clear(tasks, 0, taskCount);
            Array.Clear(owners, 0, taskCount);
            System.Buffers.ArrayPool<Task>.Shared.Return(tasks, clearArray: true);
            s_connectionPool.Return(owners, clearArray: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task AwaitBatchAsync(
        Task[] tasks,
        IConnection[] owners,
        int taskCount,
        CancellationToken cancellationToken,
        string operationName)
    {
        try
        {
            await Task.WhenAll(MemoryExtensions.AsSpan(tasks, 0, taskCount)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation($"[NW.{nameof(ConnectionHub)}:{operationName}] broadcast-cancel");
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.LogBroadcastFailures(tasks, owners, taskCount, operationName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyCapacityLimit(IConnection? newConnection, string reason)
    {
        CapacityLimitReachedHandler? handler = CapacityLimitReached;
        handler?.Invoke(
            _options.DropPolicy,
            Volatile.Read(ref _count),
            _maxConnections,
            newConnection?.ID,
            reason ?? string.Empty);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnClientDisconnected(object? sender, IConnectEventArgs args)
    {
        if (args is null)
        {
            return;
        }

        _ = this.TryUnregisterCore(args.Connection);
    }


    private enum RegisterResult : byte
    {
        Success = 0,
        Disposed = 1,
        Duplicate = 2,
        CapacityLimitReached = 3
    }

    #endregion Private Methods
}

/// <summary>
/// Delegate raised when the connection hub reaches capacity and applies a drop policy.
/// </summary>
/// <param name="dropPolicy">The active drop policy at the time the event fires.</param>
/// <param name="currentConnections">Current active connection count.</param>
/// <param name="maxConnections">Configured maximum connection count.</param>
/// <param name="triggeredConnectionId">Identifier for the incoming connection that triggered the limit.</param>
/// <param name="reason">Reason token that describes the applied action.</param>
public delegate void CapacityLimitReachedHandler(DropPolicy dropPolicy, int currentConnections, int maxConnections, ISnowflake? triggeredConnectionId, string reason);

