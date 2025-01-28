using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Network.Firewall.Models;
using Notio.Shared.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Firewall;

/// <summary>
/// Lớp xử lý giới hạn và theo dõi số lượng kết nối đồng thời từ mỗi địa chỉ IP.
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

    public ConnectionLimiter(FirewallConfig? networkConfig = null, ILogger? logger = null)
    {
        _logger = logger;
        _firewallConfig = networkConfig ?? ConfiguredShared.Instance.Get<FirewallConfig>();

        if (_firewallConfig.MaxConnectionsPerIpAddress <= 0)
            throw new InternalErrorException("MaxConnectionsPerIpAddress must be greater than 0");

        _maxConnectionsPerIp = _firewallConfig.MaxConnectionsPerIpAddress;
        _connectionInfo = new ConcurrentDictionary<string, ConnectionInfo>();
        _cleanupLock = new SemaphoreSlim(1, 1);

        _cleanupTimer = new Timer(
            async _ => await CleanupStaleConnectionsAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

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

    public (int CurrentConnections, int TotalToday, DateTime LastConnection) GetConnectionInfo(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));

        ConnectionInfo stats = _connectionInfo.GetValueOrDefault(endPoint);
        return (stats.CurrentConnections, stats.TotalConnectionsToday, stats.LastConnectionTime);
    }

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