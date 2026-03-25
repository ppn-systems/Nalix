// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
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
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("ConnectionHub (Count={_count})")]
public sealed class ConnectionHub : IConnectionHub, System.IDisposable, IReportable
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

    [System.Diagnostics.CodeAnalysis.AllowNull]
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
    public event System.Action<IConnection> ConnectionUnregistered;

    /// <summary>
    /// Raised when a limit is reached (e.g., max connections) and a connection is rejected.
    /// </summary>
    public event System.EventHandler<ConnectionHubEventArgs> CapacityLimitReached;

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
    static ConnectionHub() =>
        // Static constructor initializes the shared ArrayPool
        s_connectionPool = System.Buffers.ArrayPool<IConnection>.Shared;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionHub"/> class.
    /// </summary>
    public ConnectionHub()
    {
        _options = ConfigurationManager.Instance.Get<ConnectionHubOptions>();
        _options.Validate();

        _shardCount = System.Math.Max(1, _options.ShardCount);
        int concurrencyLevel = System.Environment.ProcessorCount * 2;

        _shards = new();

        for (int i = 0; i < _shardCount; i++)
        {
            _shards[i] = new System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection>();
        }

        _usernames = new(concurrencyLevel, _options.InitialUsernameCapacity);
        _anonymousQueue = new System.Collections.Concurrent.ConcurrentQueue<ISnowflake>();
        _usernameToId = new(concurrencyLevel, _options.InitialUsernameCapacity, System.StringComparer.OrdinalIgnoreCase);
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc />
    /// <summary>
    /// Registers a new connection with the hub.
    /// </summary>
    /// <param name="connection">The connection to register.</param>
    /// <returns><c>true</c> if the connection was successfully registered; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="connection"/> is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public bool RegisterConnection([System.Diagnostics.CodeAnalysis.NotNull] IConnection connection)
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
            _ = System.Threading.Interlocked.Increment(ref _count);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public bool UnregisterConnection([System.Diagnostics.CodeAnalysis.NotNull] IConnection connection)
    {
        if (connection is null || _disposed)
        {
            return false;
        }

        // Wait for OnCloseEvent to complete if configured
        if (_options.UnregisterDrainMillis > 0)
        {
            _ = System.Threading.Tasks.Task.Delay(_options.UnregisterDrainMillis)
                                           .ConfigureAwait(false);
        }

        TimingScope scope = default;

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        int shardIndex = GetShardIndex(connection.ID);
        System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard = _shards[shardIndex];

        if (!shard.TryRemove(connection.ID, out IConnection existing))
        {
            if (_usernames.TryRemove(connection.ID, out string orphanUser) && orphanUser is not null)
            {
                _ = _usernameToId.TryRemove(orphanUser, out _);
            }

            s_logger.Debug($"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister-miss id={connection.ID}");

            return false;
        }

        if (_usernames.TryRemove(connection.ID, out string username))
        {
            _ = _usernameToId.TryRemove(username, out _);
        }

        (existing ?? connection).OnCloseEvent -= OnClientDisconnected;

        _ = System.Threading.Interlocked.Decrement(ref _count);

        s_logger.Trace($"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister id={connection.ID} total={_count}");

        ConnectionUnregistered?.Invoke(existing ?? connection);

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
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="connection"/> or <paramref name="username"/> is null or empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "SYSLIB1045:Convert to 'GeneratedRegexAttribute'.", Justification = "<Pending>")]
    public void AssociateUsername(
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection,
        [System.Diagnostics.CodeAnalysis.NotNull] string username)
    {
        if (connection is null || string.IsNullOrWhiteSpace(username) || _disposed)
        {
            return;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-zA-Z0-9_]+$"))
        {
            throw new System.ArgumentException("Username contains invalid characters.", nameof(username));
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
        if (_usernames.TryGetValue(id, out string oldUsername) && oldUsername != username)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0018:Inline variable declaration", Justification = "<Pending>")]
    public IConnection GetConnection([System.Diagnostics.CodeAnalysis.NotNull] ISnowflake id)
    {
        IConnection connection;
        System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard = GetShard(id);

        return shard.TryGetValue(id, out connection) ? connection : null;
    }

    /// <summary>
    /// Retrieves a connection by its serialized identifier.
    /// </summary>
    /// <param name="id">The serialized identifier of the connection.</param>
    /// <returns>The connection associated with the identifier, or <c>null</c> if not found.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0018:Inline variable declaration", Justification = "<Pending>")]
    public IConnection GetConnection([System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<byte> id)
    {
        ISnowflake snowflake = Snowflake.FromBytes(id);

        IConnection connection;
        System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard = GetShard(snowflake);

        return shard.TryGetValue(snowflake, out connection) ? connection : null;
    }

    /// <summary>
    /// Retrieves a connection by its associated username.
    /// </summary>
    /// <param name="username">The username associated with the connection.</param>
    /// <returns>The connection associated with the username, or <c>null</c> if not found.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="username"/> is null or empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0018:Inline variable declaration", Justification = "<Pending>")]
    public IConnection GetConnection([System.Diagnostics.CodeAnalysis.NotNull] string username)
    {
        ISnowflake id;
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }
        else
        {
            return _usernameToId.TryGetValue(username, out id) ? GetConnection(id) : null;
        }
    }

    /// <summary>
    /// Retrieves the username associated with a connection identifier.
    /// </summary>
    /// <param name="id">The identifier of the connection.</param>
    /// <returns>The username associated with the connection, or <c>null</c> if not found.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0018:Inline variable declaration", Justification = "<Pending>")]
    public string GetUsername([System.Diagnostics.CodeAnalysis.NotNull] ISnowflake id)
    {
        string username;
        return _usernames.TryGetValue(id, out username) ? username : null;
    }

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a read-only collection of all active connections.
    /// </summary>
    /// <returns>A read-only collection of active connections.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public IReadOnlyCollection<IConnection> ListConnections()
    {
        if (_disposed || _count == 0)
        {
            return System.Array.Empty<IConnection>();
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
            System.Array.Copy(buffer, result, index);

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
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="message"/> or <paramref name="sendFunc"/> is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public async System.Threading.Tasks.Task BroadcastAsync<T>(
        [System.Diagnostics.CodeAnalysis.NotNull] T message,
        System.Func<IConnection, T, System.Threading.Tasks.Task> sendFunc,
        System.Threading.CancellationToken cancellationToken = default) where T : class
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

        List<System.Threading.Tasks.Task> tasks = [];
        foreach (IEnumerator<IConnection> partition in partitioner.GetPartitions(_shardCount))
        {
            tasks.Add(
                System.Threading.Tasks.Task.Run(async () =>
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
                            catch (System.Exception ex)
                            {
                                s_logger.Error($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastAsync)}] send-failure id={partition.Current.ID}", ex);
                            }
                        }
                    }
                }, cancellationToken));
        }

        try
        {
            await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
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
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="message"/>, <paramref name="sendFunc"/>, or <paramref name="predicate"/> is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public async System.Threading.Tasks.Task BroadcastWhereAsync<T>(
        [System.Diagnostics.CodeAnalysis.NotNull] T message,
        System.Func<IConnection, T, System.Threading.Tasks.Task> sendFunc,
        System.Func<IConnection, bool> predicate, System.Threading.CancellationToken cancellation = default) where T : class
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

        System.Threading.Tasks.Task[] tasks =
            System.Buffers.ArrayPool<System.Threading.Tasks.Task>.Shared.Rent(filteredConnections.Count);

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

            await System.Threading.Tasks.Task.WhenAll(System.MemoryExtensions
                                             .AsSpan(tasks, 0, index))
                                             .ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastWhereAsync)}] broadcast-cancel");
        }
        finally
        {
            System.Buffers.ArrayPool<System.Threading.Tasks.Task>.Shared.Return(tasks, clearArray: true);
        }
    }

    /// <summary>
    /// Forcibly closes all connections matching the specified IP address.
    /// </summary>
    /// <param name="networkEndpoint">The IP address to forcefully close.</param>
    /// <returns>Number of connections closed.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="networkEndpoint"/> is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public int ForceClose([System.Diagnostics.CodeAnalysis.NotNull] INetworkEndpoint networkEndpoint)
    {
        System.ArgumentNullException.ThrowIfNull(networkEndpoint);

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
                string connAddress = conn?.NetworkEndpoint?.Address ?? "null";

                if (connAddress != targetAddress)
                {
                    continue;
                }

                try
                {
                    conn.Disconnect("Force disconnected by IP.");
                    closedCount++;
                }
                catch (System.Exception ex)
                {
                    s_logger.Error($"[NW.{nameof(ConnectionHub)}:{nameof(ForceClose)}] disconnect failed id={conn?.ID}", ex);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void CloseAllConnections([System.Diagnostics.CodeAnalysis.AllowNull] string reason = null)
    {
        if (_disposed)
        {
            return;
        }

        IReadOnlyCollection<IConnection> connections = ListConnections();

        System.Threading.Tasks.ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = _options.ParallelDisconnectDegree
        };

        _ = System.Threading.Tasks.Parallel.ForEach(connections, parallelOptions, connection =>
        {
            try
            {
                connection.Disconnect(reason);
            }
            catch (System.Exception ex)
            {
                s_logger.Error($"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-error id={connection.ID}", ex);
            }
        });

        // Dispose all dictionaries
        _shards.Clear();
        _usernames.Clear();
        _usernameToId.Clear();
        _anonymousQueue.Clear();
        _ = System.Threading.Interlocked.Exchange(ref _count, 0);

        s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-all total={connections.Count}");
    }

    /// <summary>
    /// Generates a human-readable report of active connections and statistics.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public string GenerateReport()
    {
        const int Limit = 15;

        int count = 0;
        long sumBytesSent = 0, sumUptime = 0, maxUptime = 0, minUptime = long.MaxValue;

        System.Text.StringBuilder sb = new();
        ConnectionHubStatistics stats = Statistics;
        Dictionary<string, int> algoCounts = new(System.StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> statusCounts = new(System.StringComparer.OrdinalIgnoreCase);

        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConnectionHub Status:");
        _ = sb.AppendLine($"Total Connections    : {_count}");
        _ = sb.AppendLine($"Anonymous Users      : {_count - _usernames.Count}");
        _ = sb.AppendLine($"Authenticated Users  : {_usernames.Count}");
        _ = sb.AppendLine($"Evicted Connections  : {_evictedConnections}");
        _ = sb.AppendLine($"Rejected Connections : {_rejectedConnections}");
        _ = sb.AppendLine($"Shard Count          : {stats.ShardCount}");
        _ = sb.AppendLine($"Anonymous Queue Depth: {stats.AnonymousQueueDepth}");
        _ = sb.AppendLine($"Max Connections      : {(stats.MaxConnections < 0 ? "Unlimited" : stats.MaxConnections.ToString())}");
        _ = sb.AppendLine($"Drop Policy          : {stats.DropPolicy}");

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

        _ = sb.AppendLine($"Total Bytes Sent   : {sumBytesSent:N0}");
        _ = sb.AppendLine($"Average Uptime     : {(_count > 0 ? sumUptime / _count : 0)}s");
        _ = sb.AppendLine($"Max Connection Time: {maxUptime}s");
        _ = sb.AppendLine($"Min Connection Time: {(minUptime == long.MaxValue ? 0 : minUptime)}s");

        _ = sb.AppendLine();
        // ===== Connection Status Summary =====

        _ = sb.AppendLine("Connection Status Summary:");
        _ = sb.AppendLine("----------------------------------------");
        _ = sb.AppendLine("Status          | Count");
        _ = sb.AppendLine("----------------------------------------");

        foreach (KeyValuePair<string, int> kvp in statusCounts)
        {
            _ = sb.AppendLine($"{kvp.Key,-15} | {kvp.Value,5}");
        }

        _ = sb.AppendLine("----------------------------------------");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Algorithm Summary:");
        _ = sb.AppendLine("----------------------------------------");
        _ = sb.AppendLine("Algorithm         | Count");
        _ = sb.AppendLine("----------------------------------------");
        foreach (KeyValuePair<string, int> kvp in algoCounts)
        {
            _ = sb.AppendLine($"{kvp.Key,-16} | {kvp.Value,5}");
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

                _ = sb.AppendLine($"{id,-14} | {username}");

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private int GetShardIndex(ISnowflake id) => (id.GetHashCode() & 0x7FFFFFFF) % _shardCount;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> GetShard(ISnowflake id)
    {
        int index = GetShardIndex(id);
        return _shards[index];
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnClientDisconnected(
        [System.Diagnostics.CodeAnalysis.AllowNull] object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs args) => UnregisterConnection(args.Connection);

    [System.Diagnostics.StackTraceHidden]
    private void HandleConnectionLimit(IConnection newConnection)
    {
        s_logger.Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] connection-limit-reached policy={_options.DropPolicy} max={_options.MaxConnections}");

        switch (_options.DropPolicy)
        {
            case DropPolicy.DropNewest:
                NotifyCapacityLimit(newConnection, "drop-newest");
                newConnection.Disconnect("connection limit reached");
                _ = System.Threading.Interlocked.Increment(ref _rejectedConnections);
                break;


            case DropPolicy.DropOldest:
                while (_anonymousQueue.TryDequeue(out ISnowflake oldestId))
                {
                    int shardIndex = GetShardIndex(oldestId);
                    System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> shard = _shards[shardIndex];

                    if (shard.TryGetValue(oldestId, out IConnection oldestConn) && !_usernames.ContainsKey(oldestId))
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
                _ = System.Threading.Interlocked.Increment(ref _evictedConnections);
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
    [System.Diagnostics.StackTraceHidden]
    private async System.Threading.Tasks.Task BroadcastBatchedAsync<T>(
        IReadOnlyCollection<IConnection> connections, T message,
        System.Func<IConnection, T, System.Threading.Tasks.Task> sendFunc, System.Threading.CancellationToken cancellationToken) where T : class
    {
        int batchSize = _options.BroadcastBatchSize;
        List<System.Threading.Tasks.Task> currentBatch = new(batchSize);

        foreach (IConnection connection in connections)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            currentBatch.Add(sendFunc(connection, message));

            if (currentBatch.Count >= batchSize)
            {
                await System.Threading.Tasks.Task.WhenAll(currentBatch)
                                                 .ConfigureAwait(false);
                currentBatch.Clear();
            }
        }

        // Send remaining batch
        if (currentBatch.Count > 0)
        {
            await System.Threading.Tasks.Task.WhenAll(currentBatch)
                                             .ConfigureAwait(false);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
