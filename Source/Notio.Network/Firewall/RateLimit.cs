using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Firewall;

/// <summary>
/// Lớp xử lý giới hạn số lượng yêu cầu từ mỗi địa chỉ IP trong một cửa sổ thời gian.
/// </summary>
public sealed class RateLimit(int maxAllowedRequests, int timeWindowInMilliseconds, int lockoutDurationInSeconds)
{
    private readonly int _maxRequests = maxAllowedRequests;
    private readonly TimeSpan _timeWindow = TimeSpan.FromMilliseconds(timeWindowInMilliseconds);
    private readonly int _lockoutDuration = lockoutDurationInSeconds;

    private readonly ConcurrentDictionary<string, (Queue<DateTime> Requests, DateTime? BlockedUntil)> _ipData = new();

    /// <summary>
    /// Kiểm tra xem một địa chỉ IP có được phép gửi yêu cầu hay không, dựa trên số lượng yêu cầu đã thực hiện.
    /// </summary>
    public bool IsAllowed(string endPoint)
    {
        if (string.IsNullOrEmpty(endPoint))
            throw new ArgumentException("IP address must be a valid string.");

        DateTime currentTime = DateTime.UtcNow;

        if (_ipData.TryGetValue(endPoint, out var ipInfo) && ipInfo.BlockedUntil.HasValue)
        {
            if (currentTime < ipInfo.BlockedUntil.Value)
                return false; // IP vẫn bị khóa
            _ipData[endPoint] = (ipInfo.Requests, null); // Mở khóa
        }

        var requests = ipInfo.Requests ?? new Queue<DateTime>();

        while (requests.Count > 0 && currentTime - requests.Peek() > _timeWindow)
        {
            requests.Dequeue();
        }

        // Kiểm tra số lượng yêu cầu
        if (requests.Count < _maxRequests)
        {
            requests.Enqueue(currentTime);
            _ipData[endPoint] = (requests, ipInfo.BlockedUntil);
            return true;
        }

        // Khóa IP nếu vượt giới hạn
        _ipData[endPoint] = (requests, currentTime.AddSeconds(_lockoutDuration));
        return false;
    }

    /// <summary>
    /// Phương thức xóa các yêu cầu không hợp lệ sau một khoảng thời gian.
    /// </summary>
    public async Task ClearInactiveRequests(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);

            DateTime currentTime = DateTime.UtcNow;

            foreach (var ip in _ipData.Keys.ToList())
            {
                var ipInfo = _ipData[ip];
                while (ipInfo.Requests.Count > 0 &&
                       currentTime - ipInfo.Requests.Peek() > _timeWindow)
                {
                    ipInfo.Requests.Dequeue();
                }

                if (ipInfo.Requests.Count == 0)
                    _ipData.TryRemove(ip, out _);
                else
                    _ipData[ip] = ipInfo;
            }
        }
    }
}