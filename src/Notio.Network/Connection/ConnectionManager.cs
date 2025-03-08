using Notio.Common.Connection;
using Notio.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Notio.Network.Connection;

/// <summary>
/// Manages active network connections, providing session tracking and controlled disposal.
/// </summary>
public class ConnectionManager(ILogger logger) : IDisposable
{
    #region Fields

    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, IConnection> _activeConnections = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _isDisposed = false;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of currently active connections.
    /// </summary>
    public int ActiveConnectionCount => _activeConnections.Count;

    #endregion

    #region Methods

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
            if (_activeConnections.TryAdd(connection.Id, connection))
            {
                connection.OnCloseEvent += HandleConnectionClosed;
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
    /// <param name="sender">The connection sender.</param>
    /// <param name="e">Event arguments containing connection details.</param>
    private void HandleConnectionClosed(object? sender, IConnectEventArgs e)
    {
        if (sender is IConnection connection && _activeConnections.TryRemove(connection.Id, out _))
        {
            connection.OnCloseEvent -= HandleConnectionClosed;
            _logger.Info($"Connection closed. ID: {connection.Id}, Remaining connections: {_activeConnections.Count}");
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

            foreach (var connection in _activeConnections.Values)
            {
                try
                {
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error closing connection {connection.Id}: {ex.Message}", ex);
                }
            }

            _activeConnections.Clear();
            _logger.Info("All managed connections closed.");
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
        => _activeConnections.TryGetValue(sessionId, out IConnection? connection) ? connection : null;

    /// <summary>
    /// Determines whether a session is active.
    /// </summary>
    /// <param name="sessionId">The session ID to check.</param>
    /// <returns><c>true</c> if the session is active; otherwise, <c>false</c>.</returns>
    public bool IsSessionActive(string sessionId)
        => _activeConnections.ContainsKey(sessionId);

    #endregion

    #region Dispose Pattern

    /// <summary>
    /// Releases all resources used by the <see cref="ConnectionManager"/> instance.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        lock (this)
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }

        try
        {
            CloseAllConnections();
            _cts.Dispose();
            _logger.Info("ConnectionManager disposed.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error disposing ConnectionManager: {ex.Message}", ex);
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}
