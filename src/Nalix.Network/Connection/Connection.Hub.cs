// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Identity;
using Nalix.Framework.Injection;

namespace Nalix.Network.Connection;

/// <summary>
/// Manages connections for servers, optimized for high performance and thread safety.
/// </summary>
/// <remarks>
/// This class provides efficient connection management with minimal allocations and fast lookup operations.
/// It is thread-safe and uses concurrent collections to handle multiple connections simultaneously.
/// </remarks>
public sealed class ConnectionHub : IConnectionHub, System.IDisposable, IReportable
{
    #region Fields

    // Separate dictionaries for better cache locality and reduced contention
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IIdentifier, System.String> _usernames =
        new(System.Environment.ProcessorCount * 2, 1024);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<IIdentifier, IConnection> _connections =
        new(System.Environment.ProcessorCount * 2, 1024);

    // Username-to-ID reverse lookup for fast user-based operations
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.String, IIdentifier> _usernameToId =
        new(System.Environment.ProcessorCount * 2, 1024, System.StringComparer.OrdinalIgnoreCase);

    // Connection statistics for monitoring
    private volatile System.Int32 _connectionCount;

    private volatile System.Boolean _disposed;

    // Outbound-allocated collections for bulk operations
    private static readonly System.Buffers.ArrayPool<IConnection> s_connectionPool;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current number of active connections.
    /// </summary>
    public System.Int32 ConnectionCount => this._connectionCount;

    /// <summary>
    /// Raised after a connection is successfully unregistered.
    /// </summary>
    public event System.Action<IConnection>? ConnectionUnregistered;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes static members of the <see cref="ConnectionHub"/> class.
    /// </summary>
    static ConnectionHub() =>
        // Static constructor initializes the shared ArrayPool
        s_connectionPool = System.Buffers.ArrayPool<IConnection>.Shared;

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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean RegisterConnection(IConnection connection)
    {
        if (connection is null || this._disposed)
        {
            return false;
        }

        if (this._connections.TryAdd(connection.ID, connection))
        {
            connection.OnCloseEvent += this.OnClientDisconnected;
            _ = System.Threading.Interlocked.Increment(ref this._connectionCount);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(ConnectionHub)}] " +
                                           $"register id={connection.ID} total={this._connectionCount}");

            return true;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(ConnectionHub)}] register-dup id={connection.ID}");
        return false;
    }

    /// <inheritdoc />
    /// <summary>
    /// Unregisters a connection from the hub.
    /// </summary>
    /// <param name="connection">The connection to unregister.</param>
    /// <returns><c>true</c> if the connection was successfully unregistered; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean UnregisterConnection(IConnection connection)
    {
        if (connection is null || this._disposed)
        {
            return false;
        }

        if (!this._connections.TryRemove(connection.ID, out IConnection? existing))
        {
            if (this._usernames.TryRemove(connection.ID, out System.String? orphanUser) && orphanUser is not null)
            {
                _ = this._usernameToId.TryRemove(orphanUser, out _);
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(ConnectionHub)}] " +
                                          $"unregister-miss id={connection.ID}");
            return false;
        }

        if (this._usernames.TryRemove(connection.ID, out System.String? username))
        {
            _ = this._usernameToId.TryRemove(username, out _);
        }

        (existing ?? connection).OnCloseEvent -= this.OnClientDisconnected;

        _ = System.Threading.Interlocked.Decrement(ref this._connectionCount);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[{nameof(ConnectionHub)}] " +
                                       $"unregister id={connection.ID} total={this._connectionCount}");

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
    public void AssociateUsername(IConnection connection, System.String username)
    {
        if (connection is null || System.String.IsNullOrWhiteSpace(username) || this._disposed)
        {
            return;
        }

        var id = connection.ID;

        // Remove old association if exists
        if (this._usernames.TryGetValue(id, out System.String? oldUsername) && oldUsername != username)
        {
            _ = this._usernameToId.TryRemove(oldUsername, out _);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(ConnectionHub)}] " +
                                           $"map-rebind id={id} old={oldUsername} new={username}");
        }

        // Push new associations
        this._usernames[id] = username;
        this._usernameToId[username] = id;

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[{nameof(ConnectionHub)}] map user=\"{username}\" id={id}");
    }

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a connection by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the connection to retrieve.</param>
    /// <returns>The connection associated with the identifier, or <c>null</c> if not found.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IConnection? GetConnection(IIdentifier id)
        => this._connections.TryGetValue(id, out IConnection? connection) ? connection : null;

    /// <summary>
    /// Retrieves a connection by its serialized identifier.
    /// </summary>
    /// <param name="id">The serialized identifier of the connection.</param>
    /// <returns>The connection associated with the identifier, or <c>null</c> if not found.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IConnection? GetConnection(System.ReadOnlySpan<System.Byte> id)
        => this._connections.TryGetValue(Identifier.FromBytes(id), out IConnection? connection) ? connection : null;

    /// <summary>
    /// Retrieves a connection by its associated username.
    /// </summary>
    /// <param name="username">The username associated with the connection.</param>
    /// <returns>The connection associated with the username, or <c>null</c> if not found.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="username"/> is null or empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IConnection? GetConnectionByUsername(System.String username)
        => System.String.IsNullOrWhiteSpace(username)
        ? null : this._usernameToId.TryGetValue(username, out IIdentifier? id)
        ? this.GetConnection(id) : null;

    /// <summary>
    /// Retrieves the username associated with a connection identifier.
    /// </summary>
    /// <param name="id">The identifier of the connection.</param>
    /// <returns>The username associated with the connection, or <c>null</c> if not found.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String? GetUsername(IIdentifier id)
        => this._usernames.TryGetValue(id, out System.String? username) ? username : null;

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a read-only collection of all active connections.
    /// </summary>
    /// <returns>A read-only collection of active connections.</returns>
    public System.Collections.Generic.IReadOnlyCollection<IConnection> ListConnections()
    {
        System.Int32 count = _connectionCount;
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
    public async System.Threading.Tasks.Task BroadcastAsync<T>(
        T message,
        System.Func<IConnection, T, System.Threading.Tasks.Task> sendFunc,
        System.Threading.CancellationToken cancellationToken = default)
        where T : class
    {
        if (message is null || sendFunc is null || this._disposed)
        {
            return;
        }

        System.Collections.Generic.IReadOnlyCollection<IConnection> connections = this.ListConnections();
        if (connections is null || connections.Count == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(ConnectionHub)}] broadcast-skip total=0");
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
            await System.Threading.Tasks.Task
                      .WhenAll(System.MemoryExtensions
                      .AsSpan(tasks, 0, index)
                      .ToArray())
                      .ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(ConnectionHub)}] broadcast-cancel");
        }
    }

    /// <summary>
    /// Broadcasts a message to connections that match a specified predicate.
    /// </summary>
    /// <typeparam name="T">The type of the message to broadcast.</typeparam>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="sendFunc">The function to send the message to a connection.</param>
    /// <param name="predicate">The predicate to filter connections.</param>
    /// <param name="_">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous broadcast operation.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="message"/>, <paramref name="sendFunc"/>, or <paramref name="predicate"/> is null.</exception>
    public async System.Threading.Tasks.Task BroadcastWhereAsync<T>(
        T message,
        System.Func<IConnection, T, System.Threading.Tasks.Task> sendFunc,
        System.Func<IConnection, System.Boolean> predicate,
        System.Threading.CancellationToken _ = default)
        where T : class
    {
        if (message is null || sendFunc is null || predicate is null || this._disposed)
        {
            return;
        }

        System.Collections.Generic.List<IConnection> filteredConnections = [];
        foreach (IConnection connection in this._connections.Values)
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

        System.Threading.Tasks.Task[] tasks = new System.Threading.Tasks.Task[filteredConnections.Count];
        for (System.Int32 i = 0; i < filteredConnections.Count; i++)
        {
            IConnection connection = filteredConnections[i];
            tasks[i] = sendFunc(connection, message);
        }

        try
        {
            await System.Threading.Tasks.Task.WhenAll(tasks)
                                             .ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(ConnectionHub)}] broadcast-cancel");
        }
    }

    /// <summary>
    /// Closes all active connections with an optional reason.
    /// </summary>
    /// <param name="reason">The reason for closing the connections, if any.</param>
    public void CloseAllConnections(System.String? reason = null)
    {
        if (this._disposed)
        {
            return;
        }

        System.Collections.Generic.IReadOnlyCollection<IConnection> connections = this.ListConnections();

        _ = System.Threading.Tasks.Parallel.ForEach(connections, connection =>
        {
            try
            {
                connection.Disconnect(reason);
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(ConnectionHub)}] " +
                                               $"disconnect-error id={connection.ID}", ex);
            }
        });

        // Dispose all dictionaries
        _connections.Clear();
        _usernames.Clear();
        _usernameToId.Clear();
        _ = System.Threading.Interlocked.Exchange(ref _connectionCount, 0);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(ConnectionHub)}] disconnect-all total={connections.Count}");
    }

    /// <summary>
    /// Generates a human-readable report of active connections and statistics.
    /// </summary>
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();

        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConnectionHub Status:");
        _ = sb.AppendLine($"Total Connections   : {_connectionCount}");
        _ = sb.AppendLine($"Authenticated Users : {_usernames.Count}");
        _ = sb.AppendLine($"Anonymous Users     : {_connectionCount - _usernames.Count}");
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
    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;
        this.CloseAllConnections("disposed");

        // Unsubscribe from all events
        foreach (IConnection connection in this._connections.Values)
        {
            connection.OnCloseEvent -= this.OnClientDisconnected;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(ConnectionHub)}] disposed");
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Handles the disconnection of a client by unregistering it from the hub.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The event arguments containing the connection.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnClientDisconnected(System.Object? sender, IConnectEventArgs args) => this.UnregisterConnection(args.Connection);

    #endregion Private Methods
}