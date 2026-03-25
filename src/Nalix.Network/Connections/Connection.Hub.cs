// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Security;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration;
using Nalix.Framework.Identifiers;
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
    private readonly System.Collections.Concurrent.ConcurrentQueue<ISnowflake> _anonymousQueue;

    /// <summary>
    /// Separate dictionaries for better cache locality and reduced contention
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, string> _usernames;

    /// <summary>
    /// Username-to-ID reverse lookup for fast user-based operations
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ISnowflake> _usernameToId;

    private readonly int _shardCount;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection>> _shards;

    private readonly ConnectionHubOptions _options;

    private readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

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

    /// <summary>
    /// Gets the current statistics snapshot for this connection hub.
    /// </summary>
    public ConnectionHubStatistics Statistics =>
        new(
            connectionCount: _count,
            maxConnections: _options.MaxConnections,
            dropPolicy: _options.DropPolicy,
            shardCount: _shardCount,
            anonymousQueueDepth: _anonymousQueue.Count,
            evictedConnections: _evictedConnections,
            rejectedConnections: _rejectedConnections);

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
        int concurrencyLevel = Environment.ProcessorCount * 2;

        _shards = new();

        for (int i = 0; i < _shardCount; i++)
        {
            _shards[i] = new System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection>();
        }

        _usernames = new(concurrencyLevel, _options.InitialUsernameCapacity);
        _anonymousQueue = new System.Collections.Concurrent.ConcurrentQueue<ISnowflake>();
        _usernameToId = new(concurrencyLevel, _options.InitialUsernameCapacity, StringComparer.OrdinalIgnoreCase);
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc />
    /// <summary>
    /// Registers a new connection with the hub.
    /// </summary>
    /// <param name="connection">The connection to register.</param>
    /// <returns><c>true</c> if the connection was successfully registered; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="connection"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool RegisterConnection(IConnection connection)
    {
        if (connection is null || _disposed)
        {
            return false;
        }

        if (_options.MaxConnections > 0 && _count >= _options.MaxConnections)
        {
            HandleConnectionLimit(connection);
            return false;
        }

        TimingScope scope = default;

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        int shardIndex = GetShardIndex(connection.ID);
        System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard = _shards[shardIndex];

        if (shard.TryAdd(connection.ID, connection))
        {
            connection.OnCloseEvent += OnClientDisconnected;
            _ = Interlocked.Increment(ref _count);
            _anonymousQueue.Enqueue(connection.ID);


            s_logger.Trace($"[NW.{nameof(ConnectionHub)}:{nameof(RegisterConnection)}] register id={connection.ID} total={_count}");

            if (_options.IsEnableLatency)
            {
                s_logger.Info($"[PERF.NW.RegisterConnection] id={connection.ID}, latency={scope.GetElapsedMilliseconds():F3} ms");
            }

            return true;
        }

        s_logger.Debug($"[NW.{nameof(ConnectionHub)}:{nameof(RegisterConnection)}] register-dup id={connection.ID}");

        return false;
    }

    /// <inheritdoc />
    /// <summary>
    /// Unregisters a connection from the hub.
    /// </summary>
    /// <param name="connection">The connection to unregister.</param>
    /// <returns><c>true</c> if the connection was successfully unregistered; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool UnregisterConnection(IConnection connection)
    {
        if (connection is null || _disposed)
        {
            return false;
        }

        // Wait for OnCloseEvent to complete if configured
        if (_options.UnregisterDrainMillis > 0)
        {
            _ = Task.Delay(_options.UnregisterDrainMillis)
                                           .ConfigureAwait(false);
        }

        TimingScope scope = default;

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        int shardIndex = GetShardIndex(connection.ID);
        System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard = _shards[shardIndex];

        if (!shard.TryRemove(connection.ID, out IConnection? existing))
        {
            if (_usernames.TryRemove(connection.ID, out string? orphanUser) && orphanUser is not null)
            {
                _ = _usernameToId.TryRemove(orphanUser, out _);
            }

            s_logger.Debug($"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister-miss id={connection.ID}");

            return false;
        }

        if (_usernames.TryRemove(connection.ID, out string? username) && username is not null)
        {
            _ = _usernameToId.TryRemove(username, out _);
        }

        IConnection removedConnection = existing ?? connection;
        removedConnection.OnCloseEvent -= OnClientDisconnected;

        _ = Interlocked.Decrement(ref _count);

        s_logger.Trace($"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister id={connection.ID} total={_count}");

        ConnectionUnregistered?.Invoke(removedConnection);

        if (_options.IsEnableLatency)
        {
            s_logger.Info($"[PERF.NW.UnregisterConnection] id={connection.ID}, latency={scope.GetElapsedMilliseconds():F3} ms");
        }

        return true;
    }

    /// <inheritdoc />
    /// <summary>
    /// Associates a username with a connection.
    /// </summary>
    /// <param name="connection">The connection to associate with the username.</param>
    /// <param name="username">The username to associate.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="connection"/> or <paramref name="username"/> is null or empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Performance", "SYSLIB1045:Convert to 'GeneratedRegexAttribute'.", Justification = "<Pending>")]
    public void AssociateUsername(
        IConnection connection,
        string username)
    {
        if (connection is null || string.IsNullOrWhiteSpace(username) || _disposed)
        {
            return;
        }

        if (!Regex.IsMatch(username, "^[a-zA-Z0-9_]+$"))
        {
            throw new ArgumentException("Username contains invalid characters.", nameof(username));
        }

        // Apply username policies
        if (_options.TrimUsernames)
        {
            username = username.Trim();
        }

        if (username.Length > _options.MaxUsernameLength)
        {
            username = username[.._options.MaxUsernameLength];
        }

        ISnowflake id = connection.ID;

        // Remove old association if exists
        if (_usernames.TryGetValue(id, out string? oldUsername) && oldUsername is not null && oldUsername != username)
        {
            _ = _usernameToId.TryRemove(oldUsername, out _);


            s_logger.Trace($"[NW.{nameof(ConnectionHub)}:{nameof(AssociateUsername)}] map-rebind id={id} old={oldUsername} new={username}");

        }

        // Push new associations
        _usernames[id] = username;
        _usernameToId[username] = id;

        s_logger.Trace($"[NW.{nameof(ConnectionHub)}:{nameof(AssociateUsername)}] map user=\"{username}\" id={id}");
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
        System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard = GetShard(id);
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
        ISnowflake snowflake = Snowflake.FromBytes(id);
        System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard = GetShard(snowflake);
        return shard.TryGetValue(snowflake, out IConnection? connection) ? connection : null;
    }

    /// <summary>
    /// Retrieves a connection by its associated username.
    /// </summary>
    /// <param name="username">The username associated with the connection.</param>
    /// <returns>The connection associated with the username, or <c>null</c> if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="username"/> is null or empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: MaybeNull]
    public IConnection? GetConnection(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return _usernameToId.TryGetValue(username, out ISnowflake? id) ? GetConnection(id) : null;
    }

    /// <summary>
    /// Retrieves the username associated with a connection identifier.
    /// </summary>
    /// <param name="id">The identifier of the connection.</param>
    /// <returns>The username associated with the connection, or <c>null</c> if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: MaybeNull]
    public string? GetUsername(ISnowflake id) => _usernames.TryGetValue(id, out string? username) ? username : null;

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a read-only collection of all active connections.
    /// </summary>
    /// <returns>A read-only collection of active connections.</returns>
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
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

            foreach (System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard in _shards.Values)
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

            foreach (System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard in _shards.Values)
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
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
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

        IReadOnlyCollection<IConnection> connections = ListConnections();
        if (connections is null || connections.Count == 0)
        {
            s_logger.Trace($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastAsync)}] broadcast-skip total=0");

            return;
        }

        // Use batching if configured
        if (_options.BroadcastBatchSize > 0)
        {
            await BroadcastBatchedAsync(connections, message, sendFunc, cancellationToken)
                      .ConfigureAwait(false);

            return;
        }

        TimingScope scope = default;

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        System.Collections.Concurrent.OrderablePartitioner<IConnection> partitioner = System.Collections.Concurrent.Partitioner.Create(
            connections, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering);

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
                                s_logger.Error($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastAsync)}] send-failure id={partition.Current.ID}", ex);
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
                s_logger.Info($"[PERF.NW.BroadcastAsync] send latency={scope.GetElapsedMilliseconds():F3} ms");
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
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
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

        foreach (System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shared in _shards.Values)
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
            s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastWhereAsync)}] broadcast-cancel");
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
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    public int ForceClose(INetworkEndpoint networkEndpoint)
    {
        ArgumentNullException.ThrowIfNull(networkEndpoint);

        if (_disposed)
        {
            s_logger.Warn($"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] called on disposed instance.");

            return 0;
        }

        int closedCount = 0;
        string targetAddress = networkEndpoint.Address;

        foreach (System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard in _shards.Values)
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
                    s_logger.Error($"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] disconnect failed id={conn.ID}", ex);
                }
            }
        }

        if (closedCount > 0)
        {
            s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] closed={closedCount} ip={targetAddress}");
        }

        return closedCount;
    }

    /// <summary>
    /// Closes all active connections with an optional reason.
    /// </summary>
    /// <param name="reason">The reason for closing the connections, if any.</param>
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    public void CloseAllConnections(string reason = null)
    {
        if (_disposed)
        {
            return;
        }

        IReadOnlyCollection<IConnection> connections = ListConnections();

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
                s_logger.Error($"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-error id={connection.ID}", ex);
            }
        });

        // Dispose all dictionaries
        _shards.Clear();
        _usernames.Clear();
        _usernameToId.Clear();
        _anonymousQueue.Clear();
        _ = Interlocked.Exchange(ref _count, 0);

        s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-all total={connections.Count}");
    }

    /// <summary>
    /// Generates a human-readable report of active connections and statistics.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    public string GenerateReport()
    {
        const int Limit = 15;

        int count = 0;
        long sumBytesSent = 0, sumUptime = 0, maxUptime = 0, minUptime = long.MaxValue;

        StringBuilder sb = new();
        ConnectionHubStatistics stats = Statistics;
        Dictionary<string, int> algoCounts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> statusCounts = new(StringComparer.OrdinalIgnoreCase);

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConnectionHub Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Connections    : {_count}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Anonymous Users      : {_count - _usernames.Count}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Authenticated Users  : {_usernames.Count}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Evicted Connections  : {_evictedConnections}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Rejected Connections : {_rejectedConnections}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Shard Count          : {stats.ShardCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Anonymous Queue Depth: {stats.AnonymousQueueDepth}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Max Connections      : {(stats.MaxConnections < 0 ? "Unlimited" : stats.MaxConnections.ToString(CultureInfo.InvariantCulture))}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Drop Policy          : {stats.DropPolicy}");

        foreach (System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard in _shards.Values)
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

        foreach (System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard in _shards.Values)
        {
            foreach (KeyValuePair<ISnowflake, IConnection> kvp in shard)
            {
                ISnowflake id = kvp.Key;
                string username = GetUsername(id) ?? "(anonymous)";

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

    /// <inheritdoc />
    /// <summary>
    /// Releases all resources used by the <see cref="ConnectionHub"/> and closes all connections.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseAllConnections("disposed");

        s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(Dispose)}] disposed");
    }

    #endregion APIs

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetShardIndex(ISnowflake id) => (id.GetHashCode() & 0x7FFFFFFF) % _shardCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> GetShard(ISnowflake id)
    {
        int index = GetShardIndex(id);
        return _shards[index];
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnClientDisconnected(
        object? sender,
        IConnectEventArgs args) => UnregisterConnection(args.Connection);

    [StackTraceHidden]
    private void HandleConnectionLimit(IConnection newConnection)
    {
        s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] connection-limit-reached policy={_options.DropPolicy} max={_options.MaxConnections}");

        switch (_options.DropPolicy)
        {
            case DropPolicy.DropNewest:
                NotifyCapacityLimit(newConnection, "drop-newest");
                newConnection.Disconnect("connection limit reached");
                _ = Interlocked.Increment(ref _rejectedConnections);
                break;


            case DropPolicy.DropOldest:
                while (_anonymousQueue.TryDequeue(out ISnowflake? oldestId))
                {
                    if (oldestId is null)
                    {
                        continue;
                    }

                    int shardIndex = GetShardIndex(oldestId);
                    System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard = _shards[shardIndex];

                    if (shard.TryGetValue(oldestId, out IConnection? oldestConn) && oldestConn is not null && !_usernames.ContainsKey(oldestId))
                    {
                        s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] evicting-anonymous id={oldestConn.ID}");
                        NotifyCapacityLimit(newConnection, "evict-oldest");

                        oldestConn.Disconnect("evicted to make room for new connection");
                        return;
                    }
                }

                s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] no-anonymous-to-evict, rejecting-new");
                NotifyCapacityLimit(newConnection, "evict-oldest-no-anonymous");

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
                await Task.WhenAll(currentBatch)
                                                 .ConfigureAwait(false);
                currentBatch.Clear();
            }
        }

        // Send remaining batch
        if (currentBatch.Count > 0)
        {
            await Task.WhenAll(currentBatch)
                                             .ConfigureAwait(false);
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
            reason: reason ?? string.Empty,
            snapshot: Statistics);

        CapacityLimitReached?.Invoke(this, args);
    }

    #endregion Private Methods
}
