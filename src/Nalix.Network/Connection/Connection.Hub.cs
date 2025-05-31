using Nalix.Common.Connection;
using Nalix.Common.Identity;
using Nalix.Common.Logging;
using Nalix.Identifiers;
using Nalix.Shared.Injection.DI;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Connection;

/// <summary>
/// High-performance connection manager optimized for MMORPG servers.
/// Thread-safe with minimal allocations and efficient lookup operations.
/// </summary>
public sealed class ConnectionHub : SingletonBase<ConnectionHub>, IConnectionHub, IDisposable
{
    private ILogger? _logger;

    // Separate dictionaries for better cache locality and reduced contention
    private readonly ConcurrentDictionary<IEncodedId, string> _usernames =
        new(System.Environment.ProcessorCount * 2, 1024);

    private readonly ConcurrentDictionary<IEncodedId, IConnection> _connections =
        new(System.Environment.ProcessorCount * 2, 1024);

    // Username-to-ID reverse lookup for fast user-based operations
    private readonly ConcurrentDictionary<string, IEncodedId> _usernameToId =
        new(System.Environment.ProcessorCount * 2, 1024, StringComparer.OrdinalIgnoreCase);

    // Connection statistics for monitoring
    private volatile int _connectionCount;

    private volatile bool _disposed;

    // Pre-allocated collections for bulk operations
    private static readonly ArrayPool<IConnection> s_connectionPool = ArrayPool<IConnection>.Shared;

    /// <summary>
    /// Current number of active connections
    /// </summary>
    public int ConnectionCount => _connectionCount;

    /// <inheritdoc/>
    public void SetLogging(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RegisterConnection(IConnection connection)
    {
        if (connection is null || _disposed)
            return false;

        if (_connections.TryAdd(connection.Id, connection))
        {
            connection.OnCloseEvent += OnClientDisconnected;
            Interlocked.Increment(ref _connectionCount);

            _logger?.Info("[{0}] Connection registered: {1} (Total: {2})",
                nameof(ConnectionHub), connection.Id, _connectionCount);
            return true;
        }

        _logger?.Warn("[{0}] Connection already exists: {1}", nameof(ConnectionHub), connection.Id);
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

            connection.OnCloseEvent -= OnClientDisconnected;
            Interlocked.Decrement(ref _connectionCount);

            _logger?.Info("[{0}] Connection unregistered: {1} (Total: {2})",
                nameof(ConnectionHub), id, _connectionCount);
            return true;
        }

        _logger?.Warn("[{0}] Failed to unregister connection: {1}", nameof(ConnectionHub), id);
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IConnection? GetConnection(IEncodedId id)
        => _connections.TryGetValue(id, out var connection) ? connection : null;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IConnection? GetConnection(ReadOnlySpan<byte> id)
        => _connections.TryGetValue(Base36Id.FromByteArray(id), out var connection) ? connection : null;

    /// <summary>
    /// Get connection by username (fast lookup)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IConnection? GetConnectionByUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        return _usernameToId.TryGetValue(username, out var id) ? GetConnection(id) : null;
    }

    /// <summary>
    /// Get username for connection ID
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetUsername(IEncodedId id)
        => _usernames.TryGetValue(id, out var username) ? username : null;

    /// <inheritdoc/>
    public IReadOnlyCollection<IConnection> ListConnections()
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
            Array.Copy(connections, result, index);
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
    public async Task BroadcastAsync<T>(T message, Func<IConnection, T, Task> sendFunc, CancellationToken cancellationToken = default)
        where T : class
    {
        if (message is null || sendFunc is null || _disposed)
            return;

        var connections = ListConnections();
        if (connections.Count == 0)
            return;

        var tasks = new Task[connections.Count];
        var index = 0;

        foreach (var connection in connections)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            tasks[index++] = Task.Run(async () =>
            {
                try
                {
                    await sendFunc(connection, message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.Error("[{0}] Broadcast error for {1}: {2}", nameof(ConnectionHub), connection.Id, ex.Message);
                }
            }, cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks.AsSpan(0, index).ToArray()).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger?.Info("[{0}] Broadcast cancelled", nameof(ConnectionHub));
        }
    }

    /// <summary>
    /// Broadcast to connections matching a predicate
    /// </summary>
    public async Task BroadcastWhereAsync<T>(T message, Func<IConnection, T, Task> sendFunc,
        Func<IConnection, bool> predicate, CancellationToken cancellationToken = default)
        where T : class
    {
        if (message is null || sendFunc is null || predicate is null || _disposed)
            return;

        var filteredConnections = new List<IConnection>();
        foreach (var connection in _connections.Values)
        {
            if (predicate(connection))
                filteredConnections.Add(connection);
        }

        if (filteredConnections.Count == 0)
            return;

        var tasks = new Task[filteredConnections.Count];
        for (int i = 0; i < filteredConnections.Count; i++)
        {
            var connection = filteredConnections[i];
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    await sendFunc(connection, message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.Error("[{0}] Filtered broadcast error for {1}: {2}", nameof(ConnectionHub), connection.Id, ex.Message);
                }
            }, cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger?.Info("[{0}] Filtered broadcast cancelled", nameof(ConnectionHub));
        }
    }

    /// <inheritdoc/>
    public void CloseAllConnections(string? reason = null)
    {
        if (_disposed) return;

        var connections = ListConnections();

        Parallel.ForEach(connections, connection =>
        {
            try
            {
                connection.Disconnect(reason);
            }
            catch (Exception ex)
            {
                _logger?.Error("[{0}] Error disconnecting {1}: {2}", nameof(ConnectionHub), connection.Id, ex.Message);
            }
        });

        // Clear all dictionaries
        _connections.Clear();
        _usernames.Clear();
        _usernameToId.Clear();
        Interlocked.Exchange(ref _connectionCount, 0);

        _logger?.Info("[{0}] All {1} connections disconnected", nameof(ConnectionHub), connections.Count);
    }

    /// <summary>
    /// Get connection statistics
    /// </summary>
    public ConnectionStats GetStats()
    {
        return new ConnectionStats
        {
            TotalConnections = _connectionCount,
            AuthenticatedConnections = _usernames.Count,
            AnonymousConnections = _connectionCount - _usernames.Count
        };
    }

    private void OnClientDisconnected(object? sender, IConnectEventArgs args)
    {
        if (args.Connection is not null && !_disposed)
            UnregisterConnection(args.Connection.Id);
    }

    /// <summary>
    /// Releases unmanaged resources and performs other cleanup operations.
    /// </summary>
    public new void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        CloseAllConnections("Server shutting down");

        // Unsubscribe from all events
        foreach (var connection in _connections.Values)
        {
            connection.OnCloseEvent -= OnClientDisconnected;
        }

        _logger?.Info("[{0}] Disposed", nameof(ConnectionHub));
    }
}
