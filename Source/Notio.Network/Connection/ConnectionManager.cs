using Notio.Common.Logging.Interfaces;
using System;
using System.Collections.Concurrent;

namespace Notio.Network.Connection;

/// <summary>
/// Initializes a new instance of the ConnectionManager class.
/// </summary>
/// <param name="logger">Optional logger instance for logging events.</param>
public sealed class ConnectionManager(ILogger? logger = null) : IDisposable
{
    private readonly ConcurrentDictionary<string, Connection> _connections = new();
    private readonly ILogger? _logger = logger;
    private bool _disposed;

    /// <summary>
    /// Adds a new connection.
    /// </summary>
    /// <param name="connection">The connection to add.</param>
    /// <returns>True if added successfully; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool AddConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        bool added = _connections.TryAdd(connection.Id, connection);
        if (added)
        {
            _logger?.Info($"Connection added: {connection.Id}");
        }
        else
        {
            _logger?.Warn($"Failed to add connection: {connection.Id} already exists.");
        }
        return added;
    }

    /// <summary>
    /// Removes an existing connection.
    /// </summary>
    /// <param name="connection">The connection to remove.</param>
    /// <returns>True if removed successfully; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool RemoveConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        bool removed = _connections.TryRemove(connection.Id, out _);
        if (removed)
        {
            _logger?.Info($"Connection removed: {connection.Id}");
        }
        else
        {
            _logger?.Warn($"Failed to remove connection: {connection.Id} not found.");
        }
        return removed;
    }

    /// <summary>
    /// Broadcasts a message to all active connections.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    public void BroadcastMessage(ReadOnlySpan<byte> message)
    {
        foreach (var connection in _connections.Values)
        {
            try
            {
                connection.Send(message);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error broadcasting to connection {connection.Id}: {ex}");
            }
        }
    }

    /// <summary>
    /// Disposes the ConnectionManager and all managed connections.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var connection in _connections.Values)
            {
                try
                {
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Error disposing connection {connection.Id}: {ex}");
                }
            }
            _connections.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}