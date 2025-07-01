using Nalix.Common.Connection;
using Nalix.Common.Identity;
using Nalix.Common.Logging;
using Nalix.Identifiers;
using Nalix.Shared.Injection.DI;

namespace Nalix.Network.Connection;

/// <summary>
/// High-performance connection manager optimized for MMORPG servers.
/// Thread-safe with minimal allocations and efficient lookup operations.
/// </summary>
public sealed class ConnectionHub : SingletonBase<ConnectionHub>, IConnectionHub, System.IDisposable
{
    private ILogger? _logger;

    // Separate dictionaries for better cache locality and reduced contention
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IEncodedId, string> _usernames =
        new(System.Environment.ProcessorCount * 2, 1024);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<IEncodedId, IConnection> _connections =
        new(System.Environment.ProcessorCount * 2, 1024);

    // Username-to-ID reverse lookup for fast user-based operations
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IEncodedId> _usernameToId =
        new(System.Environment.ProcessorCount * 2, 1024, System.StringComparer.OrdinalIgnoreCase);

    // Connection statistics for monitoring
    private volatile int _connectionCount;

    private volatile bool _disposed;

    // Pre-allocated collections for bulk operations
    private static readonly System.Buffers.ArrayPool<IConnection> s_connectionPool;

    /// <summary>
    /// Current number of active connections
    /// </summary>
    public int ConnectionCount => _connectionCount;

    static ConnectionHub()
    {
        s_connectionPool = System.Buffers.ArrayPool<IConnection>.Shared;
    }

    /// <summary>
    /// Sets the logger instance to be used by the ConnectionHub.
    /// </summary>
    /// <param name="logger">The logger instance to set. Cannot be null.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if the provided logger is null.</exception>
    public void SetLogging(ILogger logger)
    {
        System.ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool RegisterConnection(IConnection connection)
    {
        if (connection is null || _disposed)
            return false;

        if (_connections.TryAdd(connection.Id, connection))
        {
            connection.OnCloseEvent += this.OnClientDisconnected;
            System.Threading.Interlocked.Increment(ref _connectionCount);

            _logger?.Info("[{0}] Connection registered: {1} (Total: {2})",
                nameof(ConnectionHub), connection.Id, _connectionCount);
            return true;
        }

        _logger?.Warn("[{0}] Connection already exists: {1}", nameof(ConnectionHub), connection.Id);
        return false;
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void AssociateUsername(IConnection connection, string username)
    {
        if (connection is null || string.IsNullOrWhiteSpace(username) || _disposed)
            return;

        var id = connection.Id;

        // Remove old association if exists
        if (_usernames.TryGetValue(id, out var oldUsername))
        {
            _usernameToId.TryRemove(oldUsername, out _);
        }

        // Add new associations
        _usernames[id] = username;
        _usernameToId[username] = id;

        _logger?.Debug("[{0}] Username associated: {1} -> {2}", nameof(ConnectionHub), username, id);
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool UnregisterConnection(IEncodedId id)
    {
        if (id is null || _disposed)
            return false;

        if (_connections.TryRemove(id, out var connection))
        {
            // Clean up username associations
            if (_usernames.TryRemove(id, out var username))
            {
                _usernameToId.TryRemove(username, out _);
            }

            connection.OnCloseEvent -= this.OnClientDisconnected;
            System.Threading.Interlocked.Decrement(ref _connectionCount);

            _logger?.Info("[{0}] Connection unregistered: {1} (Total: {2})",
                nameof(ConnectionHub), id, _connectionCount);
            return true;
        }

        _logger?.Warn("[{0}] Failed to unregister connection: {1}", nameof(ConnectionHub), id);
        return false;
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IConnection? GetConnection(IEncodedId id)
        => _connections.TryGetValue(id, out var connection) ? connection : null;

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IConnection? GetConnection(System.ReadOnlySpan<byte> id)
        => _connections.TryGetValue(Base36Id.FromByteArray(id), out var connection) ? connection : null;

    /// <summary>
    /// Get connection by username (fast lookup)
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IConnection? GetConnectionByUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        return _usernameToId.TryGetValue(username, out var id) ? this.GetConnection(id) : null;
    }

    /// <summary>
    /// Get username for connection ID
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string? GetUsername(IEncodedId id)
        => _usernames.TryGetValue(id, out var username) ? username : null;

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyCollection<IConnection> ListConnections()
    {
        var count = _connectionCount;
        if (count == 0)
            return [];

        var connections = s_connectionPool.Rent(count + 16); // Small buffer for race conditions
        try
        {
            var index = 0;
            foreach (var connection in _connections.Values)
            {
                if (index >= connections.Length) break;
                connections[index++] = connection;
            }

            var result = new IConnection[index];
            System.Array.Copy(connections, result, index);
            return result;
        }
        finally
        {
            s_connectionPool.Return(connections, clearArray: true);
        }
    }

    /// <summary>
    /// Broadcast message to all connections efficiently
    /// </summary>
    public async System.Threading.Tasks.Task BroadcastAsync<T>(
        T message,
        System.Func<IConnection, T, System.Threading.Tasks.Task> sendFunc,
        System.Threading.CancellationToken cancellationToken = default)
        where T : class
    {
        if (message is null || sendFunc is null || _disposed)
            return;

        var connections = this.ListConnections();
        if (connections.Count == 0)
            return;

        var tasks = new System.Threading.Tasks.Task[connections.Count];
        var index = 0;

        foreach (var connection in connections)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            tasks[index++] = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await sendFunc(connection, message).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    _logger?.Error("[{0}] Broadcast error for {1}: {2}", nameof(ConnectionHub), connection.Id, ex.Message);
                }
            }, cancellationToken);
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
            _logger?.Info("[{0}] Broadcast cancelled", nameof(ConnectionHub));
        }
    }

    /// <summary>
    /// Broadcast to connections matching a predicate
    /// </summary>
    public async System.Threading.Tasks.Task BroadcastWhereAsync<T>(
        T message,
        System.Func<IConnection, T, System.Threading.Tasks.Task> sendFunc,
        System.Func<IConnection, bool> predicate,
        System.Threading.CancellationToken cancellationToken = default)
        where T : class
    {
        if (message is null || sendFunc is null || predicate is null || _disposed)
            return;

        System.Collections.Generic.List<IConnection> filteredConnections = [];
        foreach (IConnection connection in _connections.Values)
        {
            if (predicate(connection))
                filteredConnections.Add(connection);
        }

        if (filteredConnections.Count == 0)
            return;

        var tasks = new System.Threading.Tasks.Task[filteredConnections.Count];
        for (int i = 0; i < filteredConnections.Count; i++)
        {
            var connection = filteredConnections[i];
            tasks[i] = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await sendFunc(connection, message).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    _logger?.Error("[{0}] Filtered broadcast error for {1}: {2}", nameof(ConnectionHub), connection.Id, ex.Message);
                }
            }, cancellationToken);
        }

        try
        {
            await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            _logger?.Info("[{0}] Filtered broadcast cancelled", nameof(ConnectionHub));
        }
    }

    /// <inheritdoc/>
    public void CloseAllConnections(string? reason = null)
    {
        if (_disposed) return;

        var connections = this.ListConnections();

        System.Threading.Tasks.Parallel.ForEach(connections, connection =>
        {
            try
            {
                connection.Disconnect(reason);
            }
            catch (System.Exception ex)
            {
                _logger?.Error("[{0}] Error disconnecting {1}: {2}", nameof(ConnectionHub), connection.Id, ex.Message);
            }
        });

        // Dispose all dictionaries
        _connections.Clear();
        _usernames.Clear();
        _usernameToId.Clear();
        System.Threading.Interlocked.Exchange(ref _connectionCount, 0);

        _logger?.Info("[{0}] All {1} connections disconnected", nameof(ConnectionHub), connections.Count);
    }

    /// <summary>
    /// Get connection statistics
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public ConnectionStats GetStats()
    {
        return new ConnectionStats
        {
            TotalConnections = _connectionCount,
            AuthenticatedConnections = _usernames.Count,
            AnonymousConnections = _connectionCount - _usernames.Count
        };
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnClientDisconnected(object? sender, IConnectEventArgs args)
    {
        if (args.Connection is not null && !_disposed)
            this.UnregisterConnection(args.Connection.Id);
    }

    /// <summary>
    /// Releases unmanaged resources and performs other cleanup operations.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public new void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        this.CloseAllConnections("Server shutting down");

        // Unsubscribe from all events
        foreach (var connection in _connections.Values)
        {
            connection.OnCloseEvent -= OnClientDisconnected;
        }

        _logger?.Info("[{0}] Disposed", nameof(ConnectionHub));
    }
}
