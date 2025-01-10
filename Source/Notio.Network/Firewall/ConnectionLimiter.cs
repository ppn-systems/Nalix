using Notio.Logging;
using Notio.Network.Firewall.Metadata;
using Notio.Shared.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Firewall;

/// <summary>
/// Lớp xử lý giới hạn và theo dõi số lượng kết nối đồng thời từ mỗi địa chỉ IP
/// </summary>
public sealed class ConnectionLimiter : IDisposable
{
    private readonly FirewallConfig _firewallConfig;
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connectionInfo;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _cleanupLock;
    private bool _disposed;

    private readonly int _maxConnectionsPerIp;

    public ConnectionLimiter(FirewallConfig? networkConfig = null)
    {
        _firewallConfig = networkConfig ?? ConfigurationShared.Instance.Get<FirewallConfig>();

        // Validate configuration
        if (_firewallConfig.MaxConnectionsPerIpAddress <= 0)
            throw new ArgumentException("MaxConnectionsPerIpAddress must be greater than 0");

        _maxConnectionsPerIp = _firewallConfig.MaxConnectionsPerIpAddress;

        _connectionInfo = new ConcurrentDictionary<string, ConnectionInfo>();
        _cleanupLock = new SemaphoreSlim(1, 1);

        // Khởi tạo timer để tự động dọn dẹp các kết nối
        _cleanupTimer = new Timer(
            async _ => await CleanupStaleConnectionsAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

    /// <summary>
    /// Kiểm tra và ghi nhận kết nối mới từ một địa chỉ IP
    /// </summary>
    /// <param name="endPoint">Địa chỉ IP cần kiểm tra</param>
    /// <returns>True nếu kết nối được chấp nhận, False nếu bị từ chối</returns>
    public bool IsConnectionAllowed(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new ArgumentException("EndPoint cannot be null or whitespace", nameof(endPoint));

        DateTime now = DateTime.UtcNow;
        DateTime currentDate = now.Date;

        if (_firewallConfig.EnableMetrics)
            NotioLog.Instance.Trace($"{endPoint}|New");

        return _connectionInfo.AddOrUpdate(
            endPoint,
            _ => new ConnectionInfo(1, now, 1, now),
            (_, stats) =>
            {
                // Reset daily counter if it's a new day
                int totalToday = currentDate > stats.LastConnectionTime.Date ? 1 : stats.TotalConnectionsToday + 1;

                // Kiểm tra các điều kiện giới hạn
                if (stats.CurrentConnections >= _maxConnectionsPerIp)
                {
                    if (_firewallConfig.EnableLogging)
                        NotioLog.Instance.Trace($"Connection limit exceeded for IP: {endPoint}");

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
    /// Cập nhật trạng thái khi một kết nối bị đóng
    /// </summary>
    public bool ConnectionClosed(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new ArgumentException("EndPoint cannot be null or whitespace", nameof(endPoint));

        if (_firewallConfig.EnableMetrics)
            NotioLog.Instance.Trace($"{endPoint}|Closed");

        bool success = _connectionInfo.AddOrUpdate(
            endPoint,
            _ => new ConnectionInfo(0, DateTime.UtcNow, 0, DateTime.UtcNow),
            (_, stats) => stats with
            {
                CurrentConnections = Math.Max(0, stats.CurrentConnections - 1),
                LastConnectionTime = DateTime.UtcNow
            }
        ).CurrentConnections >= 0;

        if (!success)
            if (_firewallConfig.EnableLogging)
                NotioLog.Instance.Trace($"Failed to close connection for IP: {endPoint}");

        return success;
    }

    /// <summary>
    /// Lấy thông tin chi tiết về kết nối của một IP
    /// </summary>
    public (int CurrentConnections, int TotalToday, DateTime LastConnection) GetConnectionInfo(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ConnectionLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new ArgumentException("EndPoint cannot be null or whitespace", nameof(endPoint));

        ConnectionInfo stats = _connectionInfo.GetValueOrDefault(endPoint);
        return (stats.CurrentConnections, stats.TotalConnectionsToday, stats.LastConnectionTime);
    }

    /// <summary>
    /// Dọn dẹp các kết nối cũ và không hoạt động
    /// </summary>
    private async Task CleanupStaleConnectionsAsync()
    {
        if (_disposed) return;

        try
        {
            await _cleanupLock.WaitAsync();
            DateTime now = DateTime.UtcNow;
            List<string> keysToRemove = [];

            foreach (var kvp in _connectionInfo)
            {
                var (ip, stats) = kvp;
                if (stats.CurrentConnections == 0)
                {
                    keysToRemove.Add(ip);

                    if (_firewallConfig.EnableLogging)
                        NotioLog.Instance.Trace($"Removing stale connection data for IP: {ip}");
                }
            }

            foreach (string key in keysToRemove)
                _connectionInfo.TryRemove(key, out _);
        }
        catch (Exception ex)
        {
            if (_firewallConfig.EnableLogging)
                NotioLog.Instance.Trace($"Error during connection cleanup: {ex.Message}");
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    /// <summary>
    /// Lấy snapshot của tất cả kết nối hiện tại
    /// </summary>
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

        _disposed = true;
        _cleanupTimer.Dispose();
        _cleanupLock.Dispose();
        _connectionInfo.Clear();
    }
}