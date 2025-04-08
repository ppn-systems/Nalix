using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Network.Configurations;
using Notio.Network.Security.Metadata;
using Notio.Shared.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Notio.Network.Security.Guard;

/// <summary>
/// A class responsible for rate-limiting requests from IP addresses to prevent abuse or excessive requests.
/// It tracks the Number of requests from each IP address within a defined time window
/// and blocks further requests when a threshold is exceeded.
/// </summary>
/// <remarks>
/// This class provides a mechanism for tracking request attempts and enforcing rate limits.
/// It uses a concurrent dictionary to track requests for each IP and enforces a lockout
/// duration when the maximum Number of requests is exceeded. A cleanup timer is used to periodically
/// remove inactive request data.
/// </remarks>
public sealed class RequestLimiter : IDisposable
{
    private readonly ILogger? _logger;
    private readonly Timer _cleanupTimer;
    private readonly RequestConfig _config;
    private readonly long _timeWindowTicks;
    private readonly long _lockoutDurationTicks;
    private readonly ConcurrentDictionary<string, RequestLimiterInfo> _ipData;

    private bool _disposed;
    private int _cleanupRunning;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLimiter"/> class with the provided firewall configuration and optional logger.
    /// </summary>
    /// <param name="config">The configuration for the firewall's rate-limiting settings. If <see langword="null"/>, the default configuration will be used.</param>
    /// <param name="logger">An optional logger for logging purposes. If <see langword="null"/>, no logging will be done.</param>
    /// <exception cref="InternalErrorException">
    /// Thrown when the configuration contains invalid rate-limiting settings.
    /// </exception>
    public RequestLimiter(RequestConfig? config = null, ILogger? logger = null)
    {
        _logger = logger;
        _config = config ?? ConfigurationStore.Instance.Get<RequestConfig>();

        if (_config.MaxAllowedRequests <= 0)
            throw new InternalErrorException("MaxAllowedRequests must be greater than 0");
        if (_config.LockoutDurationSeconds <= 0)
            throw new InternalErrorException("LockoutDurationSeconds must be greater than 0");
        if (_config.TimeWindowInMilliseconds <= 0)
            throw new InternalErrorException("TimeWindowInMilliseconds must be greater than 0");

        _ipData = new();
        _timeWindowTicks = TimeSpan.FromMilliseconds(_config.TimeWindowInMilliseconds).Ticks;
        _lockoutDurationTicks = TimeSpan.FromSeconds(_config.LockoutDurationSeconds).Ticks;

        _cleanupTimer = new Timer(static s
            => ((RequestLimiter)s!).Cleanup(), this, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger?.Debug("RequestLimiter initialized with maxRequests={0}, timeWindow={1}ms, lockout={2}s",
            _config.MaxAllowedRequests, _config.TimeWindowInMilliseconds, _config.LockoutDurationSeconds);
    }

    /// <summary>
    /// Checks the Number of requests from an IP address and determines whether further requests are allowed based on rate-limiting rules.
    /// </summary>
    /// <param name="endPoint">The IP address to check the request limit for.</param>
    /// <returns>true if the request is accepted, false if rejected.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    /// <exception cref="InternalErrorException">Thrown if the IP address is invalid.</exception>
    public bool CheckLimit([NotNull] string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RequestLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));

        long current = Stopwatch.GetTimestamp();
        RequestLimiterInfo data = _ipData.AddOrUpdate(
            endPoint,
            _ => new RequestLimiterInfo(current),
            (_, existing) => existing.Process(current, _config.MaxAllowedRequests, _timeWindowTicks, _lockoutDurationTicks)
        );

        bool allowed = data.BlockedUntilTicks < current;

        if (allowed)
        {
            _logger?.Debug("Request from {0} allowed, elapsed: {1:g}",
                            endPoint, Stopwatch.GetElapsedTime(data.LastRequestTicks));
            return allowed;
        }

        _logger?.Warn("Request from {0} blocked, elapsed: {1:g}",
                       endPoint, Stopwatch.GetElapsedTime(data.LastRequestTicks));
        return allowed;
    }

    /// <summary>
    /// Periodically cleans up inactive requests to remove expired data from the IP request tracking.
    /// </summary>
    private void Cleanup()
    {
        if (_disposed || Interlocked.Exchange(ref _cleanupRunning, 1) == 1)
            return;

        List<string> toRemove = [];
        try
        {
            long current = Stopwatch.GetTimestamp();
            foreach (var kvp in _ipData)
            {
                (string ip, RequestLimiterInfo info) = kvp;
                info.Cleanup(current, _timeWindowTicks);
                if (info.RequestCount == 0 && info.BlockedUntilTicks < current) toRemove.Add(ip);
            }
            foreach (string key in toRemove) _ipData.TryRemove(key, out _);
            _logger?.Debug("Cleanup removed {0} inactive IPs", toRemove.Count);
        }
        catch (Exception ex)
        {
            _logger?.Error("Cleanup failed: {0}", ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _cleanupRunning, 0);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _cleanupTimer.Dispose();
        _ipData.Clear();
        _logger?.Debug("RequestLimiter disposed");
    }
}
