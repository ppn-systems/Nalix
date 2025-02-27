using Notio.Common.Connection;
using Notio.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Notio.Network.Connection;

/// <summary>
/// Manages active network connections, providing session tracking and controlled disposal.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConnectionManager"/> class.
/// </remarks>
/// <param name="logger">The logger instance for logging connection events.</param>
/// <exception cref="ArgumentNullException">Thrown if <paramref name="logger"/> is null.</exception>
public class ConnectionManager(ILogger logger) : IDisposable
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, IConnection> _activeConnections = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _isDisposed = false;

    /// <summary>
    /// Gets the number of currently active connections.
    /// </summary>
    public int ActiveConnectionCount => _activeConnections.Count;

    /// <summary>
    /// Registers a new connection and tracks its session.
    /// </summary>
    /// <param name="connection">The connection to register.</param>
    /// <returns>A unique session ID for the connection.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="connection"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the connection cannot be registered.</exception>
    public Guid RegisterConnection(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            Guid sessionId = Guid.NewGuid();
            connection.OnCloseEvent += (s, e) => HandleConnectionClosed(connection.Id, e.Connection);

            if (_activeConnections.TryAdd(connection.Id, connection))
            {
                _logger.Info($"New connection registered. Session ID: {sessionId}, Total connections: {_activeConnections.Count}");
                return sessionId;
            }

            throw new InvalidOperationException("Failed to register connection.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error registering connection: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Handles the closure of a connection, removing it from active sessions.
    /// </summary>
    /// <param name="sessionId">The session ID of the connection being closed.</param>
    /// <param name="connection">The connection instance being closed.</param>
    private void HandleConnectionClosed(string sessionId, IConnection connection)
    {
        if (_activeConnections.TryRemove(sessionId, out IConnection? _))
        {
            connection.OnCloseEvent -= (s, e) => HandleConnectionClosed(sessionId, e.Connection);
            _logger.Info($"Connection closed. Session ID: {sessionId}, Remaining connections: {_activeConnections.Count}");
        }
    }

    /// <summary>
    /// Closes all active connections and clears the connection registry.
    /// </summary>
    public void CloseAllConnections()
    {
        try
        {
            _cts.Cancel();

            foreach (var connection in _activeConnections)
            {
                try
                {
                    connection.Value.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error closing connection {connection.Key}: {ex.Message}", ex);
                }
            }

            _activeConnections.Clear();
            _logger.Info("All managed connections closed by ConnectionManager.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error closing all connections: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Retrieves an active connection by its session ID.
    /// </summary>
    /// <param name="sessionId">The session ID of the connection.</param>
    /// <returns>The <see cref="IConnection"/> instance if found; otherwise, null.</returns>
    public IConnection? GetConnection(string sessionId)
    {
        _activeConnections.TryGetValue(sessionId, out IConnection? connection);
        return connection;
    }

    /// <summary>
    /// Determines whether a session is active.
    /// </summary>
    /// <param name="sessionId">The session ID to check.</param>
    /// <returns><c>true</c> if the session is active; otherwise, <c>false</c>.</returns>
    public bool IsSessionActive(string sessionId)
        => _activeConnections.ContainsKey(sessionId);

    /// <summary>
    /// Releases all resources used by the <see cref="ConnectionManager"/> instance.
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            CloseAllConnections();
            _cts.Dispose();
            _isDisposed = true;
            _logger.Info("ConnectionManager disposed.");
        }

        GC.SuppressFinalize(this);
    }
}
