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
/// Lớp quản lý và giới hạn băng thông cho mỗi kết nối
/// </summary>
public sealed class BandwidthLimiter : IDisposable
{
    private readonly ILogger? _logger;
    private readonly Timer _resetTimer;
    private readonly TimeSpan _resetInterval;
    private readonly RateLimitInfo _uploadLimit;
    private readonly RateLimitInfo _downloadLimit;
    private readonly FirewallConfig _firewallConfig;
    private readonly ConcurrentDictionary<string, BandwidthInfo> _stats;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _throttles;

    private bool _disposed;

    public BandwidthLimiter(FirewallConfig? networkConfig = null, ILogger? logger = null)
    {
        _logger = logger;
        _firewallConfig = networkConfig ?? ConfiguredShared.Instance.Get<FirewallConfig>();

        // Kiểm tra cấu hình
        if (_firewallConfig.MaxUploadBytesPerSecond <= 0 || _firewallConfig.MaxDownloadBytesPerSecond <= 0)
            throw new ArgumentException("Bandwidth limits must be greater than 0");

        _uploadLimit = new RateLimitInfo(
            _firewallConfig.MaxUploadBytesPerSecond,
            _firewallConfig.UploadBurstSize
        );

        _downloadLimit = new RateLimitInfo(
            _firewallConfig.MaxDownloadBytesPerSecond,
            _firewallConfig.DownloadBurstSize
        );

        _stats = new ConcurrentDictionary<string, BandwidthInfo>();
        _throttles = new ConcurrentDictionary<string, SemaphoreSlim>();
        _resetInterval = TimeSpan.FromSeconds(_firewallConfig.BandwidthResetIntervalSeconds);

        // Tạo timer để reset số liệu định kỳ
        _resetTimer = new Timer(
            _ => ResetBandwidthStats(),
            null,
            _resetInterval,
            _resetInterval
        );
    }

    /// <summary>
    /// Kiểm tra và ghi nhận dữ liệu gửi đi
    /// </summary>
    public async Task<bool> TryUploadAsync(string endPoint, int byteCount, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(BandwidthLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));
        if (byteCount <= 0)
            throw new InternalErrorException("Byte count must be greater than 0", nameof(byteCount));

        var throttle = _throttles.GetOrAdd(endPoint, _ => new SemaphoreSlim(_uploadLimit.BurstSize));

        try
        {
            if (!await throttle.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken))
            {
                if (_firewallConfig.EnableLogging)
                    _logger?.Trace($"Upload throttled for IP: {endPoint}");

                return false;
            }

            BandwidthInfo stats = _stats.AddOrUpdate(
                endPoint,
                _ => new BandwidthInfo(byteCount, 0, DateTime.UtcNow, DateTime.UtcNow),
                (_, current) =>
                {
                    long newTotal = current.BytesSent + byteCount;
                    if (newTotal > _uploadLimit.BytesPerSecond)
                    {
                        if (_firewallConfig.EnableLogging)
                            _logger?.Trace($"Upload limit exceeded for IP: {endPoint}");

                        return current;
                    }
                    return current with
                    {
                        BytesSent = newTotal,
                        LastActivityTime = DateTime.UtcNow
                    };
                }
            );

            if (_firewallConfig.EnableMetrics)
                _logger?.Meta($"{endPoint}|{stats.BytesSent}|Upload");

            return stats.BytesSent <= _uploadLimit.BytesPerSecond;
        }
        finally
        {
            throttle.Release();
        }
    }

    /// <summary>
    /// Kiểm tra và ghi nhận dữ liệu nhận về
    /// </summary>
    public async Task<bool> TryDownloadAsync(string endPoint, int byteCount, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(BandwidthLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));
        if (byteCount <= 0)
            throw new InternalErrorException("Byte count must be greater than 0", nameof(byteCount));

        SemaphoreSlim throttle = _throttles.GetOrAdd(endPoint, _ => new SemaphoreSlim(_downloadLimit.BurstSize));

        try
        {
            if (!await throttle.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken))
            {
                if (_firewallConfig.EnableLogging)
                    _logger?.Trace($"Download throttled for IP: {endPoint}");

                return false;
            }

            var stats = _stats.AddOrUpdate(
                endPoint,
                _ => new BandwidthInfo(0, byteCount, DateTime.UtcNow, DateTime.UtcNow),
                (_, current) =>
                {
                    var newTotal = current.BytesReceived + byteCount;
                    if (newTotal > _downloadLimit.BytesPerSecond)
                    {
                        if (_firewallConfig.EnableLogging)
                            _logger?.Trace($"Download limit exceeded for IP: {endPoint}");

                        return current;
                    }
                    return current with
                    {
                        BytesReceived = newTotal,
                        LastActivityTime = DateTime.UtcNow
                    };
                }
            );

            if (_firewallConfig.EnableMetrics)
                _logger?.Meta($"{endPoint}|{stats.BytesReceived}|Download");

            return stats.BytesReceived <= _downloadLimit.BytesPerSecond;
        }
        finally
        {
            throttle.Release();
        }
    }

    /// <summary>
    /// Lấy thông tin băng thông hiện tại của một IP
    /// </summary>
    public (long BytesSent, long BytesReceived, DateTime LastActivity) GetBandwidthInfo(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(BandwidthLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));

        var stats = _stats.GetValueOrDefault(endPoint);
        return (stats.BytesSent, stats.BytesReceived, stats.LastActivityTime);
    }

    /// <summary>
    /// Reset số liệu thống kê băng thông định kỳ
    /// </summary>
    private void ResetBandwidthStats()
    {
        if (_disposed) return;

        var now = DateTime.UtcNow;
        foreach (var kvp in _stats)
        {
            if (now - kvp.Value.LastResetTime >= _resetInterval)
            {
                _stats.TryUpdate(
                    kvp.Key,
                    kvp.Value with
                    {
                        BytesSent = 0,
                        BytesReceived = 0,
                        LastResetTime = now
                    },
                    kvp.Value
                );
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _resetTimer.Dispose();
        foreach (var throttle in _throttles.Values)
        {
            throttle.Dispose();
        }
        _throttles.Clear();
        _stats.Clear();
    }
}