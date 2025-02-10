using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Network.Firewall.Models;
using Notio.Shared.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Firewall.Rules;

/// <summary>
/// A class responsible for limiting and tracking the number of simultaneous connections from each IP address.
/// </summary>
public sealed class ConnectionLimiter : IDisposable
{
    private readonly ILogger? _logger;
    private readonly Timer _cleanupTimer;
    private readonly int _maxConnectionsPerIp;
    private readonly SemaphoreSlim _cleanupLock;
    private readonly FirewallConfig _firewallConfig;
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connectionInfo;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionLimiter"/> class with the specified firewall configuration and logger.
    /// </summary>
    /// <param name="networkConfig">The firewall configuration. If null, the default configuration is used.</param>
    /// <param name="logger">The logger used for logging connection events. If null, no logging is performed.</param>
    /// <exception cref="InternalErrorException">Thrown if <paramref name="networkConfig"/> specifies a max connections per IP address less than or equal to 0.</exception>
    public ConnectionLimiter(FirewallConfig? networkConfig = null, ILogger? logger = null)
    {
        _logger = logger;
        _firewallConfig = networkConfig ?? ConfiguredShared.Instance.Get<FirewallConfig>();

        if (_firewallConfig.Connection.MaxConnectionsPerIpAddress <= 0)
            throw new InternalErrorException("MaxConnectionsPerIpAddress must be greater than 0");

        _maxConnectionsPerIp = _firewallConfig.Connection.MaxConnectionsPerIpAddress;
        _connectionInfo = new ConcurrentDictionary<string, ConnectionInfo>();
        _cleanupLock = new SemaphoreSlim(1, 1);

        _cleanupTimer = new Timer(
            async _ => await CleanupStaleConnectionsAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

    /// <summary>
    /// Determines whether a new connection is allowed for the specified IP address.
    /// </summary>
    /// <param name="endPoint">The IP address or endpoint to check.</param>
    /// <returns><c>true</c> if the connection is allowed; otherwise, <c>false</c>.</returns>
    /// <exception cref="InternalErrorException">Thrown if <paramref name="endPoint"/> is null or whitespace.</exception>
    public bool IsConnectionAllowed(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));

        DateTime now = DateTime.UtcNow;
        DateTime currentDate = now.Date;

        if (_firewallConfig.EnableMetrics)
            _logger?.Trace($"{endPoint}|New");

        return _connectionInfo.AddOrUpdate(
            endPoint,
            _ => new ConnectionInfo(1, now, 1, now),
            (_, stats) =>
            {
                int totalToday = currentDate > stats.LastConnectionTime.Date ? 1 : stats.TotalConnectionsToday + 1;

                if (stats.CurrentConnections >= _maxConnectionsPerIp)
                {
                    if (_firewallConfig.EnableLogging)
                        _logger?.Trace($"Connection limit exceeded for IP: {endPoint}");
                    return stats;
                }

                return stats with
                {
                    CurrentConnections = stats.CurrentConnections + 1,
                    LastConnectionTime = now,
                    TotalConnectionsToday = totalToday
                };
            }
        ).CurrentConnections <= _maxConnectionsPerIp;
    }

    /// <summary>
    /// Marks a connection as closed for the specified IP address.
    /// </summary>
    /// <param name="endPoint">The IP address or endpoint to mark as closed.</param>
    /// <returns><c>true</c> if the connection was successfully marked as closed; otherwise, <c>false</c>.</returns>
    /// <exception cref="InternalErrorException">Thrown if <paramref name="endPoint"/> is null or whitespace.</exception>
    public bool ConnectionClosed(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));

        if (_firewallConfig.EnableMetrics)
            _logger?.Trace($"{endPoint}|Closed");

        return _connectionInfo.AddOrUpdate(
            endPoint,
            _ => new ConnectionInfo(0, DateTime.UtcNow, 0, DateTime.UtcNow),
            (_, stats) => stats with
            {
                CurrentConnections = Math.Max(0, stats.CurrentConnections - 1),
                LastConnectionTime = DateTime.UtcNow
            }
        ).CurrentConnections >= 0;
    }

    /// <summary>
    /// Retrieves the connection statistics for the specified IP address.
    /// </summary>
    /// <param name="endPoint">The IP address or endpoint to retrieve statistics for.</param>
    /// <returns>A tuple containing the current number of connections, total connections today, and the last connection time.</returns>
    /// <exception cref="InternalErrorException">Thrown if <paramref name="endPoint"/> is null or whitespace.</exception>
    public (int CurrentConnections, int TotalToday, DateTime LastConnection) GetConnectionInfo(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));

        ConnectionInfo stats = _connectionInfo.GetValueOrDefault(endPoint);
        return (stats.CurrentConnections, stats.TotalConnectionsToday, stats.LastConnectionTime);
    }

    /// <summary>
    /// Cleans up stale connection data that has no active connections.
    /// </summary>
    private async Task CleanupStaleConnectionsAsync()
    {
        if (_disposed) return;

        try
        {
            await _cleanupLock.WaitAsync();
            DateTime now = DateTime.UtcNow;
            var keysToRemove = new List<string>();

            foreach (var kvp in _connectionInfo)
            {
                var (ip, stats) = kvp;
                if (stats.CurrentConnections == 0)
                {
                    keysToRemove.Add(ip);

                    if (_firewallConfig.EnableLogging)
                        _logger?.Trace($"Removing stale connection data for IP: {ip}");
                }
            }

            foreach (string key in keysToRemove)
                _connectionInfo.TryRemove(key, out _);
        }
        catch (Exception ex)
        {
            if (_firewallConfig.EnableLogging)
                _logger?.Error($"Error during connection cleanup: {ex.Message}");
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    /// <summary>
    /// Retrieves the connection statistics for all IP addresses.
    /// </summary>
    /// <returns>A dictionary containing the current and total connections for each IP address.</returns>
    public IReadOnlyDictionary<string, (int Current, int Total)> GetAllConnections()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionLimiter));

        var result = new Dictionary<string, (int Current, int Total)>();
        foreach (var kvp in _connectionInfo)
        {
            result[kvp.Key] = (kvp.Value.CurrentConnections, kvp.Value.TotalConnectionsToday);
        }
        return result;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="ConnectionLimiter"/> instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _disposed = true;
            _cleanupTimer.Dispose();
            _cleanupLock.Dispose();
            _connectionInfo.Clear();
        }
        catch (Exception ex)
        {
            _logger?.Error($"Dispose error: {ex.Message}");
        }
    }
}
