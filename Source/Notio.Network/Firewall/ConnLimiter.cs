using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Notio.Network.Firewall;

/// <summary>
/// Lớp xử lý giới hạn số lượng kết nối đồng thời từ mỗi địa chỉ IP.
/// </summary>
public sealed class ConnLimiter(int _maxConnectionsPerIpAddress)
{
    private readonly int _maxConnectionsPerIp = _maxConnectionsPerIpAddress;
    private readonly ConcurrentDictionary<string, int> _ipConnectionCounts = new();

    /// <summary>
    /// Kiểm tra xem kết nối từ địa chỉ IP có được phép hay không, dựa trên số lượng kết nối hiện tại.
    /// </summary>
    /// <param name="endPoint">Địa chỉ IP cần kiểm tra.</param>
    /// <returns>True nếu kết nối được phép, False nếu không.</returns>
    public bool IsConnectionAllowed(string endPoint)
    {
        if (string.IsNullOrEmpty(endPoint)) return false;
        if (GetCurrentConnectionCount(endPoint) >= _maxConnectionsPerIp) return false;

        int newConnectionCount = _ipConnectionCounts.AddOrUpdate(
            endPoint,
            1, 
            (key, oldValue) => oldValue + 1 
        );

        return newConnectionCount <= _maxConnectionsPerIp;
    }

    /// <summary>
    /// Phương thức gọi khi kết nối bị đóng từ một địa chỉ IP.
    /// </summary>
    /// <param name="endPoint">Địa chỉ IP cần cập nhật sau khi kết nối đóng.</param>
    public bool ConnectionClosed(string endPoint)
    {
        if (string.IsNullOrEmpty(endPoint)) return false;

        _ipConnectionCounts.AddOrUpdate(endPoint, 0, (key, currentCount) =>
        {
            int newCount = currentCount - 1;
            if (newCount == 0)
            {
                return 0;  
            }

            return newCount;
        });

        return true;
    }

    /// <summary>
    /// Lấy số lượng kết nối hiện tại của một IP.
    /// </summary>
    /// <param name="endPoint">Địa chỉ IP cần lấy số lượng kết nối.</param>
    /// <returns>Số lượng kết nối hiện tại.</returns>
    public int GetCurrentConnectionCount(string endPoint) =>
        _ipConnectionCounts.GetValueOrDefault(endPoint, 0);
}