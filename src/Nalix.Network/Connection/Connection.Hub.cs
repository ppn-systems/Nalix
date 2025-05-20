using Nalix.Common.Connection;
using Nalix.Common.Identity;
using Nalix.Common.Logging;
using Nalix.Shared.Injection.DI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Nalix.Network.Connection;

/// <summary>
/// Manages active network connections in a thread-safe manner.
/// </summary>
public sealed class ConnectionHub(ILogger? logger = null) : SingletonBase<ConnectionHub>, IConnectionHub
{
    private ILogger? _logger = logger;
    private readonly ConcurrentDictionary<IEncodedId, IConnection> _connections = new();

    /// <inheritdoc/>
    public void SetLogging(ILogger logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public bool RegisterConnection(IConnection connection)
    {
        if (connection == null) return false;

        bool added = _connections.TryAdd(connection.Id, connection);

        if (added)
        {
            connection.OnCloseEvent += OnClientDisconnected;
            _logger?.Info("[{0}] Connection added: {1}", nameof(ConnectionHub), connection.Id);
        }
        else
        {
            _logger?.Warn("[{0}] Connection already exists: {1}", nameof(ConnectionHub), connection.Id);
        }

        return added;
    }

    /// <inheritdoc/>
    public bool UnregisterConnection(IEncodedId id)
    {
        if (_connections.TryRemove(id, out IConnection? connection))
        {
            connection.OnCloseEvent -= OnClientDisconnected;
            _logger?.Info("[{0}] Connection removed: {1}", nameof(ConnectionHub), id);
            return true;
        }

        _logger?.Warn("[{0}] Failed to remove connection: {1}", nameof(ConnectionHub), id);
        return false;
    }

    /// <inheritdoc/>
    public IConnection? GetConnection(IEncodedId id)
        => _connections.TryGetValue(id, out IConnection? connection) ? connection : null;

    /// <inheritdoc/>
    public IReadOnlyCollection<IConnection> ListConnections() =>
        _connections.Values.ToList().AsReadOnly();

    /// <inheritdoc/>
    public void CloseAllConnections(string? reason = null)
    {
        foreach (IConnection conn in _connections.Values)
        {
            try
            {
                conn.Disconnect(reason);
            }
            catch (Exception ex)
            {
                _logger?.Error("[{0}] DisconnectAll error for {1}: {2}", nameof(ConnectionHub), conn.Id, ex.Message);
            }
        }

        _connections.Clear();
        _logger?.Info("[{0}] All connections disconnected", nameof(ConnectionHub));
    }

    private void OnClientDisconnected(object? sender, IConnectEventArgs args)
    {
        if (args.Connection != null)
            this.UnregisterConnection(args.Connection.Id);
    }
}
