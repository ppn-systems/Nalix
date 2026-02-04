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
using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Network.Configurations;

namespace Nalix.Network.Connections;

/// <summary>
/// Manages connections for servers, optimized for high performance and thread safety.
/// </summary>
/// <remarks>
/// This class provides efficient connection management with minimal allocations and fast lookup operations.
/// It is thread-safe and uses concurrent collections to handle multiple connections simultaneously.
/// </remarks>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("ConnectionHub (Count={_count})")]
public sealed class ConnectionHub : IConnectionHub, IDisposable, IReportable
{
    #region Fields

    /// <summary>
    /// Queue tracking order of anonymous connections for O(1)-amortized eviction
    /// </summary>
    private readonly ConcurrentQueue<UInt56> _anonymousQueue;

    private readonly int _shardCount;
    private readonly ConcurrentDictionary<UInt56, IConnection>[] _shards;

    private readonly ConnectionHubOptions _options;

    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// Connections statistics for monitoring
    /// </summary>
    private volatile int _count;
    private volatile bool _disposed;
    private volatile int _evictedConnections;
    private volatile int _rejectedConnections;

    /// <summary>
    /// Outbound-allocated collections for bulk operations
    /// </summary>
    private static readonly System.Buffers.ArrayPool<IConnection> s_connectionPool;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current number of active connections.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Raised after a connection is successfully unregistered.
    /// </summary>
    public event Action<IConnection>? ConnectionUnregistered;

    /// <summary>
    /// Raised when a limit is reached (e.g., max connections) and a connection is rejected.
    /// </summary>
    public event EventHandler<ConnectionHubEventArgs>? CapacityLimitReached;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes static members of the <see cref="ConnectionHub"/> class.
    /// </summary>
    static ConnectionHub() => s_connectionPool = System.Buffers.ArrayPool<IConnection>.Shared;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionHub"/> class.
    /// </summary>
    public ConnectionHub()
    {
        _options = ConfigurationManager.Instance.Get<ConnectionHubOptions>();
        _options.Validate();

        _shardCount = Math.Max(1, _options.ShardCount);
        _anonymousQueue = new ConcurrentQueue<UInt56>();
        _shards = new ConcurrentDictionary<UInt56, IConnection>[_shardCount];

        for (int i = 0; i < _shardCount; i++)
        {
            _shards[i] = new ConcurrentDictionary<UInt56, IConnection>();
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

        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionHub));

        if (_options.MaxConnections > 0 && _count >= _options.MaxConnections)
        {
            this.HandleConnectionLimit(connection);
            throw new InvalidOperationException(
                $"Connection hub capacity reached. Maximum connections: {_options.MaxConnections}.");
        }

        TimingScope scope = default;

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        UInt56 connectionKey = connection.ID.ToUInt56();
        int shardIndex = this.GetShardIndex(connectionKey);
        ConcurrentDictionary<UInt56, IConnection> shard = _shards[shardIndex];

        if (!shard.TryAdd(connectionKey, connection))
        {
            s_logger?.Debug($"[NW.{nameof(ConnectionHub)}:{nameof(RegisterConnection)}] register-dup id={connection.ID}");
            throw new InvalidOperationException($"Connection '{connection.ID}' is already registered.");
        }

        connection.OnCloseEvent += this.OnClientDisconnected;
        _ = Interlocked.Increment(ref _count);
        _anonymousQueue.Enqueue(connectionKey);

        s_logger?.Trace($"[NW.{nameof(ConnectionHub)}:{nameof(RegisterConnection)}] register id={connection.ID} total={_count}");

        if (_options.IsEnableLatency)
        {
            s_logger?.Info($"[PERF.NW.RegisterConnection] id={connection.ID}, latency={scope.GetElapsedMilliseconds():F3} ms");
        }
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

        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionHub));

        TimingScope scope = default;

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        UInt56 connectionKey = connection.ID.ToUInt56();
        int shardIndex = this.GetShardIndex(connectionKey);
        ConcurrentDictionary<UInt56, IConnection> shard = _shards[shardIndex];

        if (!shard.TryRemove(connectionKey, out IConnection? existing))
        {
            s_logger?.Debug($"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister-miss id={connection.ID}");
            throw new InvalidOperationException($"Connection '{connection.ID}' is not registered.");
        }

        IConnection removedConnection = existing ?? connection;
        removedConnection.OnCloseEvent -= this.OnClientDisconnected;

        _ = Interlocked.Decrement(ref _count);

        s_logger?.Trace($"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister id={connection.ID} total={_count}");

        ConnectionUnregistered?.Invoke(removedConnection);

        if (_options.IsEnableLatency)
        {
            s_logger?.Info($"[PERF.NW.UnregisterConnection] id={connection.ID}, latency={scope.GetElapsedMilliseconds():F3} ms");
        }
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

        UInt56 key = id.ToUInt56();
        ConcurrentDictionary<UInt56, IConnection> shard = this.GetShard(key);
        return shard.TryGetValue(key, out IConnection? connection) ? connection : null;
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
        UInt56 key = UInt56.ReadBytesLittleEndian(id);
        ConcurrentDictionary<UInt56, IConnection> shard = this.GetShard(key);
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
        if (_disposed || _count == 0)
        {
            return Array.Empty<IConnection>();
        }

        if (_count < 10000)
        {
            List<IConnection> connections = [];

            foreach (ConcurrentDictionary<UInt56, IConnection> shard in _shards)
            {
                connections.AddRange(shard.Values);
            }

            return connections.AsReadOnly();
        }

        int estimatedCount = _count;
        IConnection[] buffer = s_connectionPool.Rent(estimatedCount);

        try
        {
            int index = 0;

            foreach (ConcurrentDictionary<UInt56, IConnection> shard in _shards)
            {
                foreach (IConnection connection in shard.Values)
                {
                    if (index >= buffer.Length)
                    {
                        break;
                    }

                    buffer[index++] = connection;
                }
            }

            IConnection[] result = new IConnection[index];
            Array.Copy(buffer, result, index);

            return result;
        }
        finally
        {
            s_connectionPool.Return(buffer, clearArray: true);
        }
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
    public async Task BroadcastAsync<T>(
        T message,
        Func<IConnection, T, Task> sendFunc,
        CancellationToken cancellationToken = default) where T : class
    {
        if (message is null || sendFunc is null || _disposed)
        {
            return;
        }

        if (_disposed)
        {
            return;
        }

        IReadOnlyCollection<IConnection> connections = this.ListConnections();
        if (connections is null || connections.Count == 0)
        {
            s_logger?.Trace($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastAsync)}] broadcast-skip total=0");

            return;
        }

        // Use batching if configured
        if (_options.BroadcastBatchSize > 0)
        {
            await this.BroadcastBatchedAsync(connections, message, sendFunc, cancellationToken)
                      .ConfigureAwait(false);

            return;
        }

        TimingScope scope = default;

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        OrderablePartitioner<IConnection> partitioner = Partitioner.Create(
            connections, EnumerablePartitionerOptions.NoBuffering);

        List<Task> tasks = [];
        foreach (IEnumerator<IConnection> partition in partitioner.GetPartitions(_shardCount))
        {
            tasks.Add(
                Task.Run(async () =>
                {
                    using (partition)
                    {
                        while (partition.MoveNext())
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            try
                            {
                                await sendFunc(partition.Current, message).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                s_logger?.Error($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastAsync)}] send-failure id={partition.Current.ID}", ex);
                            }
                        }
                    }
                }, cancellationToken));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            if (_options.IsEnableLatency)
            {
                s_logger?.Info($"[PERF.NW.BroadcastAsync] send latency={scope.GetElapsedMilliseconds():F3} ms");
            }
        }
    }

    /// <summary>
    /// Broadcasts a message to connections that match a specified predicate.
    /// </summary>
    /// <typeparam name="T">The type of the message to broadcast.</typeparam>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="sendFunc">The function to send the message to a connection.</param>
    /// <param name="predicate">The predicate to filter connections.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous broadcast operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="message"/>, <paramref name="sendFunc"/>, or <paramref name="predicate"/> is null.</exception>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public async Task BroadcastWhereAsync<T>(
        T message,
        Func<IConnection, T, Task> sendFunc,
        Func<IConnection, bool> predicate, CancellationToken cancellation = default) where T : class
    {
        if (message is null || sendFunc is null || predicate is null || _disposed)
        {
            return;
        }

        List<IConnection> filteredConnections = [];

        foreach (ConcurrentDictionary<UInt56, IConnection> shared in _shards)
        {
            foreach (IConnection connection in shared.Values)
            {
                if (predicate(connection))
                {
                    filteredConnections.Add(connection);
                }
            }
        }

        if (filteredConnections.Count == 0)
        {
            return;
        }

        Task[] tasks =
            System.Buffers.ArrayPool<Task>.Shared.Rent(filteredConnections.Count);

        try
        {
            int index = 0;
            foreach (IConnection connection in filteredConnections)
            {
                if (cancellation.IsCancellationRequested)
                {
                    break;
                }

                tasks[index++] = sendFunc(connection, message);
            }

            await Task.WhenAll(MemoryExtensions
                                             .AsSpan(tasks, 0, index))
                                             .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            s_logger?.Info($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastWhereAsync)}] broadcast-cancel");
        }
        finally
        {
            System.Buffers.ArrayPool<Task>.Shared.Return(tasks, clearArray: true);
        }
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
            s_logger?.Warn($"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] called on disposed instance.");

            return 0;
        }

        int closedCount = 0;
        string targetAddress = networkEndpoint.Address;

        foreach (ConcurrentDictionary<UInt56, IConnection> shard in _shards)
        {
            foreach (IConnection conn in shard.Values)
            {
                string connAddress = conn.NetworkEndpoint.Address;

                if (connAddress != targetAddress)
                {
                    continue;
                }

                try
                {
                    conn.Disconnect("Force disconnected by IP.");
                    closedCount++;
                }
                catch (Exception ex)
                {
                    s_logger?.Error($"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] disconnect failed id={conn.ID}", ex);
                }
            }
        }

        if (closedCount > 0)
        {
            s_logger?.Info($"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] closed={closedCount} ip={targetAddress}");
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
        if (_disposed)
        {
            return;
        }

        IReadOnlyCollection<IConnection> connections = this.ListConnections();

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = _options.ParallelDisconnectDegree
        };

        _ = Parallel.ForEach(connections, parallelOptions, connection =>
        {
            try
            {
                connection.Disconnect(reason);
            }
            catch (Exception ex)
            {
                s_logger?.Error($"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-error id={connection.ID}", ex);
            }
        });

        // Dispose all dictionaries
        foreach (ConcurrentDictionary<UInt56, IConnection> shard in _shards)
        {
            shard.Clear();
        }
        _anonymousQueue.Clear();
        _ = Interlocked.Exchange(ref _count, 0);

        s_logger?.Info($"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-all total={connections.Count}");
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

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConnectionHub Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Connections    : {_count}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Evicted Connections  : {_evictedConnections}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Rejected Connections : {_rejectedConnections}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Shard Count          : {_shardCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Anonymous Queue Depth: {_anonymousQueue.Count}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Max Connections      : {(_options.MaxConnections < 0 ? "Unlimited" : _options.MaxConnections.ToString(CultureInfo.InvariantCulture))}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Drop Policy          : {_options.DropPolicy}");

        foreach (ConcurrentDictionary<UInt56, IConnection> shard in _shards)
        {
            foreach (IConnection conn in shard.Values)
            {
                // Bytes sent
                sumBytesSent += conn.BytesSent;

                // Uptime
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
                statusCounts[status] = statusCounts.TryGetValue(status, out int current) ? current + 1 : 1;
            }
        }

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Bytes Sent   : {sumBytesSent:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Uptime     : {(_count > 0 ? sumUptime / _count : 0)}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Max Connection Time: {maxUptime}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Min Connection Time: {(minUptime == long.MaxValue ? 0 : minUptime)}s");

        _ = sb.AppendLine();
        // ===== Connection Status Summary =====

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

        // ===== Active Connections =====
        _ = sb.AppendLine("Active Connections:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine("ID             | Username");
        _ = sb.AppendLine("------------------------------------------------------------");

        foreach (ConcurrentDictionary<UInt56, IConnection> shard in _shards)
        {
            foreach (KeyValuePair<UInt56, IConnection> kvp in shard)
            {
                UInt56 id = kvp.Key;
                string username = kvp.Value.Attributes.TryGetValue("username", out object? name) && name is string s ? s : "N/A";
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
    public IDictionary<string, object> GenerateReportData()
    {
        Dictionary<string, object> report = new()
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["TotalConnections"] = _count,
            ["EvictedConnections"] = _evictedConnections,
            ["RejectedConnections"] = _rejectedConnections,
            ["ShardCount"] = _shardCount,
            ["AnonymousQueueDepth"] = _anonymousQueue.Count,
            ["MaxConnections"] = _options.MaxConnections,
            ["DropPolicy"] = _options.DropPolicy.ToString(),
        };

        // Connection metrics summary
        long sumBytesSent = 0, sumUptime = 0, maxUptime = 0, minUptime = long.MaxValue;
        Dictionary<string, int> algoCounts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> statusCounts = new(StringComparer.OrdinalIgnoreCase);

        int limit = 15, current = 0;
        List<Dictionary<string, object>> sampleConnections = [];

        foreach (ConcurrentDictionary<UInt56, IConnection> shard in _shards)
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
        report["AverageUptimeSeconds"] = _count > 0 ? (sumUptime / _count) : 0;
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

        this.CloseAllConnections("disposed");
        _disposed = true;

        s_logger?.Info($"[NW.{nameof(ConnectionHub)}:{nameof(Dispose)}] disposed");
    }

    #endregion APIs

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetShardIndex(UInt56 id) => (id.GetHashCode() & 0x7FFFFFFF) % _shardCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ConcurrentDictionary<UInt56, IConnection> GetShard(UInt56 id)
    {
        int index = this.GetShardIndex(id);
        return _shards[index];
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnClientDisconnected(
        object? sender,
        IConnectEventArgs args)
    {
        try
        {
            this.UnregisterConnection(args.Connection);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [StackTraceHidden]
    private void HandleConnectionLimit(IConnection newConnection)
    {
        s_logger?.Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] connection-limit-reached policy={_options.DropPolicy} max={_options.MaxConnections}");

        switch (_options.DropPolicy)
        {
            case DropPolicy.DropNewest:
                this.NotifyCapacityLimit(newConnection, "drop-newest");
                newConnection.Disconnect("connection limit reached");
                _ = Interlocked.Increment(ref _rejectedConnections);
                break;


            case DropPolicy.DropOldest:
                while (_anonymousQueue.TryDequeue(out UInt56 oldestId))
                {
                    int shardIndex = this.GetShardIndex(oldestId);
                    ConcurrentDictionary<UInt56, IConnection> shard = _shards[shardIndex];

                    if (shard.TryGetValue(oldestId, out IConnection? oldestConn) && oldestConn is not null)
                    {
                        s_logger?.Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] evicting-anonymous id={oldestConn.ID}");
                        this.NotifyCapacityLimit(newConnection, "evict-oldest");

                        oldestConn.Disconnect("evicted to make room for new connection");
                        return;
                    }
                }

                s_logger?.Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] no-anonymous-to-evict, rejecting-new");
                this.NotifyCapacityLimit(newConnection, "evict-oldest-no-anonymous");

                newConnection.Disconnect("connection limit reached, no anonymous connections to evict");
                _ = Interlocked.Increment(ref _evictedConnections);
                break;


            case DropPolicy.Block:
                break;
            case DropPolicy.Coalesce:
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Broadcasts a message using batching to reduce memory pressure.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="connections"></param>
    /// <param name="message"></param>
    /// <param name="sendFunc"></param>
    /// <param name="cancellationToken"></param>
    [StackTraceHidden]
    private async Task BroadcastBatchedAsync<T>(
        IReadOnlyCollection<IConnection> connections, T message,
        Func<IConnection, T, Task> sendFunc, CancellationToken cancellationToken) where T : class
    {
        int batchSize = _options.BroadcastBatchSize;
        List<Task> currentBatch = new(batchSize);

        foreach (IConnection connection in connections)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            currentBatch.Add(sendFunc(connection, message));

            if (currentBatch.Count >= batchSize)
            {
                await Task.WhenAll(currentBatch).ConfigureAwait(false);
                currentBatch.Clear();
            }
        }

        // Send remaining batch
        if (currentBatch.Count > 0)
        {
            await Task.WhenAll(currentBatch).ConfigureAwait(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NotifyCapacityLimit(IConnection newConnection, string reason)
    {
        ConnectionHubEventArgs args = new(
            dropPolicy: _options.DropPolicy,
            currentConnections: _count,
            maxConnections: _options.MaxConnections,
            triggeredConnectionId: newConnection?.ID,
            reason: reason ?? string.Empty);

        CapacityLimitReached?.Invoke(this, args);
    }

    #endregion Private Methods
}

/// <summary>
/// Event arguments raised when a capacity limit is hit.
/// </summary>
/// <param name="dropPolicy"></param>
/// <param name="currentConnections"></param>
/// <param name="maxConnections"></param>
/// <param name="triggeredConnectionId"></param>
/// <param name="reason"></param>
/// <remarks>
/// Initializes a new instance of the <see cref="ConnectionHubEventArgs"/> class.
/// </remarks>
public sealed class ConnectionHubEventArgs(
    DropPolicy dropPolicy,
    int currentConnections,
    int maxConnections,
    ISnowflake? triggeredConnectionId,
    string reason) : EventArgs
{
    /// <summary>
    /// Gets the active drop policy when the limit fired.
    /// </summary>
    public DropPolicy DropPolicy { get; } = dropPolicy;

    /// <summary>
    /// Gets the number of registered connections when the limit was reached.
    /// </summary>
    public int CurrentConnections { get; } = currentConnections;

    /// <summary>
    /// Gets the configured maximum number of connections.
    /// </summary>
    public int MaxConnections { get; } = maxConnections;

    /// <summary>
    /// Gets the connection that triggered the limit (may be null if not available).
    /// </summary>
    public ISnowflake? TriggeredConnectionId { get; } = triggeredConnectionId;

    /// <summary>
    /// Gets the textual reason for the limit notification.
    /// </summary>
    public string Reason { get; } = reason ?? string.Empty;
}
