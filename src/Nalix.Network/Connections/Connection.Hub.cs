// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Connection;
using Nalix.Common.Diagnostics;
using Nalix.Common.Enums;
using Nalix.Common.Infrastructure.Connection;
using Nalix.Framework.Configuration;
using Nalix.Framework.Identity;
using Nalix.Framework.Injection;
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

    // Queue tracking order of anonymous connections for O(1)-amortized eviction
    private readonly System.Collections.Concurrent.ConcurrentQueue<ISnowflake> _anonymousQueue;

    // Separate dictionaries for better cache locality and reduced contention
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, IConnection> _connections;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, System.String> _usernames;

    // Username-to-ID reverse lookup for fast user-based operations
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.String, ISnowflake> _usernameToId;

    private readonly ConnectionHubOptions _options;

    // Connections statistics for monitoring
    private volatile System.Int32 _count;
    private volatile System.Boolean _disposed;

    // Outbound-allocated collections for bulk operations
    private static readonly System.Buffers.ArrayPool<IConnection> s_connectionPool;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current number of active connections.
    /// </summary>
    public System.Int32 Count => _count;

    /// <summary>
    /// Raised after a connection is successfully unregistered.
    /// </summary>
    public event System.Action<IConnection> ConnectionUnregistered;

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

        System.Int32 concurrencyLevel = System.Environment.ProcessorCount * 2;

        _usernames = new(concurrencyLevel, _options.InitialUsernameCapacity);
        _connections = new(concurrencyLevel, _options.InitialConnectionCapacity);
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
    public System.Boolean RegisterConnection([System.Diagnostics.CodeAnalysis.NotNull] IConnection connection)
    {
        if (connection is null || _disposed)
        {
            return false;
        }

        if (_count >= _options.MaxConnections || _options.MaxConnections < 0)
        {
            this.HandleConnectionLimit(connection);
            return false;
        }

        if (_connections.TryAdd(connection.ID, connection))
        {
            connection.OnCloseEvent += this.OnClientDisconnected;
            _ = System.Threading.Interlocked.Increment(ref _count);
            _anonymousQueue.Enqueue(connection.ID);

            if (_options.EnableTraceLogs)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[NW.{nameof(ConnectionHub)}:{nameof(RegisterConnection)}] register id={connection.ID} total={_count}");
            }

            return true;
        }

        if (_options.EnableTraceLogs)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[NW.{nameof(ConnectionHub)}:{nameof(RegisterConnection)}] register-dup id={connection.ID}");
        }

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
    public System.Boolean UnregisterConnection([System.Diagnostics.CodeAnalysis.NotNull] IConnection connection)
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

        if (!_connections.TryRemove(connection.ID, out IConnection existing))
        {
            if (_usernames.TryRemove(connection.ID, out System.String orphanUser) && orphanUser is not null)
            {
                _ = _usernameToId.TryRemove(orphanUser, out _);
            }

            if (_options.EnableTraceLogs)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister-miss id={connection.ID}");
            }

            return false;
        }

        if (_usernames.TryRemove(connection.ID, out System.String username))
        {
            _ = _usernameToId.TryRemove(username, out _);
        }

        (existing ?? connection).OnCloseEvent -= this.OnClientDisconnected;

        _ = System.Threading.Interlocked.Decrement(ref _count);

        if (_options.EnableTraceLogs)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[NW.{nameof(ConnectionHub)}:{nameof(UnregisterConnection)}] unregister id={connection.ID} total={_count}");
        }

        ConnectionUnregistered?.Invoke(existing ?? connection);

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
        [System.Diagnostics.CodeAnalysis.NotNull] System.String username)
    {
        if (connection is null || System.String.IsNullOrWhiteSpace(username) || _disposed)
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
        if (_usernames.TryGetValue(id, out System.String oldUsername) && oldUsername != username)
        {
            _ = _usernameToId.TryRemove(oldUsername, out _);

            if (_options.EnableTraceLogs)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[NW.{nameof(ConnectionHub)}:{nameof(AssociateUsername)}] map-rebind id={id} old={oldUsername} new={username}");
            }
        }

        // Push new associations
        _usernames[id] = username;
        _usernameToId[username] = id;

        if (_options.EnableTraceLogs)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[NW.{nameof(ConnectionHub)}:{nameof(AssociateUsername)}] map user=\"{username}\" id={id}");
        }
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
        return this._connections.TryGetValue(id, out connection) ? connection : null;
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
    public IConnection GetConnection([System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> id)
    {
        IConnection connection;
        return this._connections.TryGetValue(Snowflake.FromBytes(id), out connection) ? connection : null;
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
    public IConnection GetConnection([System.Diagnostics.CodeAnalysis.NotNull] System.String username)
    {
        ISnowflake id;
        return System.String.IsNullOrWhiteSpace(username) ? null : (this._usernameToId.TryGetValue(username, out id) ? this.GetConnection(id) : null);
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
    public System.String GetUsername([System.Diagnostics.CodeAnalysis.NotNull] ISnowflake id)
    {
        System.String username;
        return this._usernames.TryGetValue(id, out username) ? username : null;
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
    public System.Collections.Generic.IReadOnlyCollection<IConnection> ListConnections()
    {
        System.Int32 count = _count;
        if (count == 0)
        {
            return [];
        }

        IConnection[] connections = s_connectionPool.Rent(count + 16); // Small buffer for race conditions
        try
        {
            System.Int32 index = 0;
            foreach (IConnection connection in _connections.Values)
            {
                if (index >= connections.Length)
                {
                    break;
                }

                connections[index++] = connection;
            }

            IConnection[] result = new IConnection[index];
            System.Array.Copy(connections, result, index);
            return result;
        }
        finally
        {
            s_connectionPool.Return(connections, clearArray: true);
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

        System.Collections.Generic.IReadOnlyCollection<IConnection> connections = this.ListConnections();
        if (connections is null || connections.Count == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastAsync)}] broadcast-skip total=0");
            return;
        }

        // Use batching if configured
        if (_options.BroadcastBatchSize > 0)
        {
            await this.BroadcastBatchedAsync(connections, message, sendFunc, cancellationToken)
                      .ConfigureAwait(false);
            return;
        }

        System.Threading.Tasks.Task[] tasks = new System.Threading.Tasks.Task[connections.Count];
        System.Int32 index = 0;

        foreach (IConnection connection in connections)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            tasks[index++] = sendFunc(connection, message);
        }

        try
        {
            if (index == 0)
            {
                return;
            }

            if (index == tasks.Length)
            {
                await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
            }
            else
            {
                System.Threading.Tasks.Task[] slice = new System.Threading.Tasks.Task[index];
                System.Array.Copy(tasks, slice, index);
                await System.Threading.Tasks.Task.WhenAll(slice)
                                                 .ConfigureAwait(false);
            }
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastAsync)}] broadcast-cancel");
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
        System.Func<IConnection, System.Boolean> predicate, System.Threading.CancellationToken cancellation = default) where T : class
    {
        if (message is null || sendFunc is null || predicate is null || _disposed)
        {
            return;
        }

        System.Collections.Generic.List<IConnection> filteredConnections = [];
        foreach (IConnection connection in _connections.Values)
        {
            if (predicate(connection))
            {
                filteredConnections.Add(connection);
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
            System.Int32 index = 0;
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
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[NW.{nameof(ConnectionHub)}:{nameof(BroadcastWhereAsync)}] broadcast-cancel");
        }
        finally
        {
            System.Buffers.ArrayPool<System.Threading.Tasks.Task>.Shared.Return(tasks, clearArray: true);
        }
    }

    /// <summary>
    /// Closes all active connections with an optional reason.
    /// </summary>
    /// <param name="reason">The reason for closing the connections, if any.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void CloseAllConnections([System.Diagnostics.CodeAnalysis.AllowNull] System.String reason = null)
    {
        if (_disposed)
        {
            return;
        }

        System.Collections.Generic.IReadOnlyCollection<IConnection> connections = this.ListConnections();

        System.Threading.Tasks.ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = _options.ParallelDisconnectDegree ?? -1
        };

        _ = System.Threading.Tasks.Parallel.ForEach(connections, parallelOptions, connection =>
        {
            try
            {
                connection.Disconnect(reason);
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-error id={connection.ID}", ex);
            }
        });

        // Dispose all dictionaries
        _connections.Clear();
        _usernames.Clear();
        _usernameToId.Clear();
        _ = System.Threading.Interlocked.Exchange(ref _count, 0);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[NW.{nameof(ConnectionHub)}:{nameof(CloseAllConnections)}] disconnect-all total={connections.Count}");
    }

    /// <summary>
    /// Generates a human-readable report of active connections and statistics.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();

        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConnectionHub Status:");
        _ = sb.AppendLine($"Total Connections   : {_count}");
        _ = sb.AppendLine($"Authenticated Users : {_usernames.Count}");
        _ = sb.AppendLine($"Anonymous Users     : {_count - _usernames.Count}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Active Connections:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine("ID                                   | Username");
        _ = sb.AppendLine("------------------------------------------------------------");

        foreach (var kvp in _connections)
        {
            var id = kvp.Key;
            var username = GetUsername(id) ?? "(anonymous)";
            _ = sb.AppendLine($"{id,-15} | {username}");
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
        this.CloseAllConnections("disposed");

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[NW.{nameof(ConnectionHub)}:{nameof(Dispose)}] disposed");
    }

    #endregion APIs

    #region Private Methods

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnClientDisconnected(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs args) => this.UnregisterConnection(args.Connection);

    [System.Diagnostics.StackTraceHidden]
    private void HandleConnectionLimit(IConnection newConnection)
    {
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] connection-limit-reached policy={_options.RejectPolicy} max={_options.MaxConnections}");

        switch (_options.RejectPolicy)
        {
            case RejectPolicy.REJECT_NEW:
                newConnection.Disconnect("connection limit reached");
                break;

            case RejectPolicy.DROP_OLDEST_ANONYMOUS:
                // Efficient eviction: use queue of anonymous IDs and dequeue until we find a valid anonymous to evict.
                while (_anonymousQueue.TryDequeue(out ISnowflake oldestId))
                {
                    // if the ID is still present and still anonymous (no username mapped) -> evict
                    if (_connections.TryGetValue(oldestId, out IConnection oldestConn) && !_usernames.ContainsKey(oldestId))
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] evicting-anonymous id={oldestConn.ID}");

                        oldestConn.Disconnect("evicted to make room for new connection");
                        return;
                    }

                    // otherwise continue to next queued id (stale or already authenticated)
                }

                // No anonymous connections found, reject new connection instead
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[NW.{nameof(ConnectionHub)}:{nameof(HandleConnectionLimit)}] no-anonymous-to-evict, rejecting-new");

                newConnection.Disconnect("connection limit reached, no anonymous connections to evict");
                break;
        }
    }

    /// <summary>
    /// Broadcasts a message using batching to reduce memory pressure.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    private async System.Threading.Tasks.Task BroadcastBatchedAsync<T>(
        System.Collections.Generic.IReadOnlyCollection<IConnection> connections, T message,
        System.Func<IConnection, T, System.Threading.Tasks.Task> sendFunc, System.Threading.CancellationToken cancellationToken) where T : class
    {
        System.Int32 batchSize = _options.BroadcastBatchSize;
        System.Collections.Generic.List<System.Threading.Tasks.Task> currentBatch = new(batchSize);

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

    #endregion Private Methods
}