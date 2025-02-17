using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Shared.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Firewall;

/// <summary>
/// A class responsible for rate-limiting requests from IP addresses to prevent abuse or excessive requests.
/// It tracks the number of requests from each IP address within a defined time window
/// and blocks further requests when a threshold is exceeded.
/// </summary>
/// <remarks>
/// This class provides a mechanism for tracking request attempts and enforcing rate limits.
/// It uses a concurrent dictionary to track requests for each IP and enforces a lockout
/// duration when the maximum number of requests is exceeded. A cleanup timer is used to periodically
/// remove inactive request data.
/// </remarks>
public sealed class RequestLimiter : IDisposable
{
    private readonly ILogger? _logger;
    private readonly Timer _cleanupTimer;
    private readonly int _maxAllowedRequests;
    private readonly SemaphoreSlim _cleanupLock;
    private readonly int _lockoutDurationSeconds;
    private readonly TimeSpan _timeWindowDuration;
    private readonly FirewallConfig _firewallConfig;
    private readonly ConcurrentDictionary<string, RequestLimiterInfo> _ipData;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLimiter"/> class with the provided firewall configuration and optional logger.
    /// </summary>
    /// <param name="networkConfig">The configuration for the firewall's rate-limiting settings. If <see langword="null"/>, the default configuration will be used.</param>
    /// <param name="logger">An optional logger for logging purposes. If <see langword="null"/>, no logging will be done.</param>
    /// <exception cref="InternalErrorException">
    /// Thrown when the configuration contains invalid rate-limiting settings.
    /// </exception>
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
        _ipData = new ConcurrentDictionary<string, RequestLimiterInfo>();
        _cleanupLock = new SemaphoreSlim(1, 1);

        // Initialize the cleanup timer for automatic request cleanup
        _cleanupTimer = new Timer(
            async void (_) => await CleanupInactiveRequestsAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

    /// <summary>
    /// Checks the number of requests from an IP address and determines whether further requests are allowed based on rate-limiting rules.
    /// </summary>
    /// <param name="endPoint">The IP address to check the request limit for.</param>
    /// <returns>true if the request is accepted, false if rejected.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown if the IP address is invalid.</exception>
    public bool CheckLimit(string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RequestLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));

        var currentTime = DateTime.UtcNow;

        bool status = _ipData.AddOrUpdate(
            endPoint,
            _ => new RequestLimiterInfo(new Queue<DateTime>([currentTime]), null),
            (_, data) => ProcessRequest(data, currentTime)
        ).BlockedUntil?.CompareTo(currentTime) <= 0;

        if (_firewallConfig.EnableMetrics)
            _logger?.Meta($"{endPoint}|{status}|{currentTime.Minute}ms");

        return status;
    }

    private RequestLimiterInfo ProcessRequest(RequestLimiterInfo data, DateTime currentTime)
    {
        var (requests, blockedUntil) = data;

        // Check if the IP is currently blocked
        if (blockedUntil.HasValue && currentTime < blockedUntil.Value)
            return data;

        // Remove old requests
        while (requests.Count > 0 && currentTime - requests.Peek() > _timeWindowDuration)
            requests.Dequeue();

        // If the maximum requests are reached, block the IP for the lockout duration
        if (requests.Count >= _maxAllowedRequests)
            return new RequestLimiterInfo(requests, currentTime.AddSeconds(_lockoutDurationSeconds));

        requests.Enqueue(currentTime);
        return new RequestLimiterInfo(requests, null);
    }

    /// <summary>
    /// Periodically cleans up inactive requests to remove expired data from the IP request tracking.
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
                    _ipData.TryUpdate(ip, new RequestLimiterInfo(cleanedRequests, data.BlockedUntil), data);
            }

            foreach (var key in keysToRemove)
                _ipData.TryRemove(key, out _);
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _cleanupTimer.Dispose();
        _cleanupLock.Dispose();
        _ipData.Clear();
    }
}

/// <summary>
/// Represents the data of a request, including the history of request timestamps and optional block expiration time.
/// </summary>
internal readonly record struct RequestLimiterInfo(
    Queue<DateTime> Requests,
    DateTime? BlockedUntil
);
