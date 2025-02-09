using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Network.Firewall.Models;
using Notio.Shared.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Firewall.Rules;

/// <summary>
/// Lớp xử lý giới hạn tốc độ yêu cầu với các tính năng nâng cao về hiệu suất và bảo mật
/// </summary>
public sealed class RequestLimiter : IDisposable
{
    private readonly ILogger? _logger;
    private readonly Timer _cleanupTimer;
    private readonly int _maxAllowedRequests;
    private readonly SemaphoreSlim _cleanupLock;
    private readonly int _lockoutDurationSeconds;
    private readonly TimeSpan _timeWindowDuration;
    private readonly FirewallConfig _firewallConfig;
    private readonly ConcurrentDictionary<string, RequestDataInfo> _ipData;

    private bool _disposed;

    public RequestLimiter(FirewallConfig? networkConfig, ILogger? logger = null)
    {
        _logger = logger;
        _firewallConfig = networkConfig ?? ConfiguredShared.Instance.Get<FirewallConfig>();

        if (_firewallConfig.RateLimit.MaxAllowedRequests <= 0)
            throw new InternalErrorException("MaxAllowedRequests must be greater than 0");

        if (_firewallConfig.RateLimit.LockoutDurationSeconds <= 0)
            throw new InternalErrorException("LockoutDurationSeconds must be greater than 0");

        if (_firewallConfig.RateLimit.TimeWindowInMilliseconds <= 0)
            throw new InternalErrorException("TimeWindowInMilliseconds must be greater than 0");

        _maxAllowedRequests = _firewallConfig.RateLimit.MaxAllowedRequests;
        _lockoutDurationSeconds = _firewallConfig.RateLimit.LockoutDurationSeconds;
        _timeWindowDuration = TimeSpan.FromMilliseconds(_firewallConfig.RateLimit.TimeWindowInMilliseconds);
        _ipData = new ConcurrentDictionary<string, RequestDataInfo>();
        _cleanupLock = new SemaphoreSlim(1, 1);

        // Khởi tạo timer để tự động dọn dẹp
        _cleanupTimer = new Timer(
            async _ => await CleanupInactiveRequestsAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

    /// <summary>
    /// Kiểm tra và ghi nhận yêu cầu từ một địa chỉ IP
    /// </summary>
    /// <param name="endPoint">Địa chỉ IP cần kiểm tra</param>
    /// <returns>true nếu yêu cầu được chấp nhận, false nếu bị từ chối</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object is disposed</exception>
    public bool CheckLimit(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RequestLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));

        var currentTime = DateTime.UtcNow;

        bool status = _ipData.AddOrUpdate(
            endPoint,
            _ => new RequestDataInfo(new Queue<DateTime>([currentTime]), null),
            (_, data) => ProcessRequest(data, currentTime)
        ).BlockedUntil?.CompareTo(currentTime) <= 0;

        if (_firewallConfig.EnableMetrics)
            _logger?.Meta($"{endPoint}|{status}|{currentTime.Minute}ms");

        return status;
    }

    private RequestDataInfo ProcessRequest(RequestDataInfo data, DateTime currentTime)
    {
        var (requests, blockedUntil) = data;

        // Kiểm tra nếu IP đang bị khóa
        if (blockedUntil.HasValue && currentTime < blockedUntil.Value)
            return data;

        // Loại bỏ các yêu cầu cũ
        while (requests.Count > 0 && currentTime - requests.Peek() > _timeWindowDuration)
            requests.Dequeue();

        // Kiểm tra và cập nhật trạng thái
        if (requests.Count >= _maxAllowedRequests)
            return new RequestDataInfo(requests, currentTime.AddSeconds(_lockoutDurationSeconds));

        requests.Enqueue(currentTime);
        return new RequestDataInfo(requests, null);
    }

    /// <summary>
    /// Dọn dẹp các yêu cầu không còn hoạt động
    /// </summary>
    private async Task CleanupInactiveRequestsAsync()
    {
        if (_disposed) return;

        try
        {
            await _cleanupLock.WaitAsync();
            var currentTime = DateTime.UtcNow;
            var keysToRemove = new List<string>();

            foreach (var kvp in _ipData)
            {
                var (ip, data) = kvp;
                var cleanedRequests = new Queue<DateTime>(
                    data.Requests.Where(time => currentTime - time <= _timeWindowDuration)
                );

                if (cleanedRequests.Count == 0 && (!data.BlockedUntil.HasValue || currentTime > data.BlockedUntil.Value))
                    keysToRemove.Add(ip);
                else
                    _ipData.TryUpdate(ip, new RequestDataInfo(cleanedRequests, data.BlockedUntil), data);
            }

            foreach (var key in keysToRemove)
                _ipData.TryRemove(key, out _);
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _cleanupTimer.Dispose();
        _cleanupLock.Dispose();
        _ipData.Clear();
    }
}