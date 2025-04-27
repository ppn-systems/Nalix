using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Security.Metadata;
using Nalix.Shared.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Security.Guard;

/// <summary>
/// A high-performance connection limiter that restricts simultaneous connections from IP addresses
/// to prevent abuse and resource exhaustion.
/// </summary>
public sealed class ConnectionLimiter : IDisposable
{
    #region Constants

    // LZ4Constants for optimization
    private const int MaxCleanupKeys = 1000;

    private const int EstimatedCollectionCapacity = 256;

    #endregion Constants

    #region Fields

    private static readonly DateTime DateTimeUnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILogger? _logger;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _cleanupLock;
    private readonly ConnLimitConfig _config;
    private readonly ConcurrentDictionary<string, ConnectionLimitInfo> _connectionInfo;

    // Cache frequently accessed configuration values
    private readonly int _maxConnectionsPerIp;

    private readonly TimeSpan _inactivityThreshold;

    private bool _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionLimiter"/> class.
    /// </summary>
    /// <param name="connectionConfig">The connection configuration. If null, default config is used.</param>
    /// <param name="logger">Optional logger for metrics and diagnostics.</param>
    /// <exception cref="ArgumentException">Thrown when configuration has invalid values.</exception>
    public ConnectionLimiter(ConnLimitConfig? connectionConfig = null, ILogger? logger = null)
    {
        _logger = logger;
        _config = connectionConfig ?? ConfigurationStore.Instance.Get<ConnLimitConfig>();

        if (_config.MaxConnectionsPerIpAddress <= 0)
            throw new ArgumentException("MaxConnectionsPerIpAddress must be greater than 0",
                nameof(connectionConfig));

        // Cache configuration values for performance
        _maxConnectionsPerIp = _config.MaxConnectionsPerIpAddress;
        _inactivityThreshold = _config.InactivityThreshold;

        // Initialize with case-insensitive string comparer for IP addresses
        _connectionInfo = new ConcurrentDictionary<string, ConnectionLimitInfo>(
            StringComparer.OrdinalIgnoreCase);
        _cleanupLock = new SemaphoreSlim(1, 1);

        // Start cleanup timer with configured interval
        _cleanupTimer = new Timer(
            async _ => await CleanupStaleConnectionsAsync().ConfigureAwait(false),
            null,
            _config.CleanupInterval,
            _config.CleanupInterval
        );

        _logger?.Debug("ConnectionLimiter initialized: max={0}, inactivity={1}s",
                        _maxConnectionsPerIp, _inactivityThreshold.TotalSeconds);
    }

    /// <summary>
    /// Initializes with default configuration and logger.
    /// </summary>
    public ConnectionLimiter(ILogger? logger = null)
        : this((ConnLimitConfig?)null, logger)
    {
    }

    /// <summary>
    /// Initializes with custom configuration via action callback.
    /// </summary>
    public ConnectionLimiter(Action<ConnLimitConfig>? configure = null, ILogger? logger = null)
        : this(CreateConfiguredConfig(configure), logger)
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Determines whether a new connection is allowed for the specified IP address.
    /// </summary>
    /// <param name="endPoint">The IP address or endpoint to check.</param>
    /// <returns><c>true</c> if the connection is allowed; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown if endpoint is null or empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnectionAllowed([NotNull] string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new ArgumentException("EndPoint cannot be null or whitespace", nameof(endPoint));

        DateTime now = DateTime.UtcNow;
        DateTime currentDate = now.Date;

        // CheckLimit if endpoint already exists
        if (_connectionInfo.TryGetValue(endPoint, out var existingInfo))
        {
            // Fast path for already at limit
            if (existingInfo.CurrentConnections >= _maxConnectionsPerIp)
            {
                _logger?.Trace("Limit exceeded for {0}: {1}/{2}",
                    endPoint, existingInfo.CurrentConnections, _maxConnectionsPerIp);

                return false;
            }

            // Fast path for typical case
            int totalToday = currentDate > existingInfo.LastConnectionTime.Date ? 1 : existingInfo.TotalConnectionsToday + 1;

            var newInfo = existingInfo with
            {
                CurrentConnections = existingInfo.CurrentConnections + 1,
                LastConnectionTime = now,
                TotalConnectionsToday = totalToday
            };

            _connectionInfo[endPoint] = newInfo;
            _logger?.Trace("Allowed {0}", endPoint);

            return true;
        }

        // New endpoint
        var info = new ConnectionLimitInfo(1, now, 1, now);
        _connectionInfo[endPoint] = info;
        _logger?.Trace("Allowed {0}", endPoint);

        return true;
    }

    /// <summary>
    /// Marks a connection as closed for the specified IP address.
    /// </summary>
    /// <param name="endPoint">The IP address or endpoint.</param>
    /// <returns>True if successfully marked as closed.</returns>
    /// <exception cref="ArgumentException">Thrown if endpoint is null or empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConnectionClosed([NotNull] string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new ArgumentException("EndPoint cannot be null or whitespace", nameof(endPoint));

        // Fast path if entry doesn't exist
        if (!_connectionInfo.TryGetValue(endPoint, out var existingInfo)) return false;

        // Update the current connections count
        var newInfo = existingInfo with
        {
            CurrentConnections = Math.Max(0, existingInfo.CurrentConnections - 1),
            LastConnectionTime = DateTime.UtcNow
        };

        _connectionInfo[endPoint] = newInfo;
        _logger?.Trace("Closed {0}", endPoint);

        return true;
    }

    /// <summary>
    /// Gets connection information for the specified IP address.
    /// </summary>
    /// <param name="endPoint">The IP address or endpoint.</param>
    /// <returns>Connection statistics tuple.</returns>
    /// <exception cref="ArgumentException">Thrown if endpoint is null or empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int CurrentConnections, int TotalToday, DateTime LastConnection) GetConnectionInfo(
        [NotNull] string endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new ArgumentException("EndPoint cannot be null or whitespace", nameof(endPoint));

        if (_connectionInfo.TryGetValue(endPoint, out var stats))
        {
            _logger?.Trace("Stats for {0}: {1} current, {2} today",
                endPoint, stats.CurrentConnections, stats.TotalConnectionsToday);
            return (stats.CurrentConnections, stats.TotalConnectionsToday, stats.LastConnectionTime);
        }

        return (0, 0, DateTimeUnixEpoch);
    }

    /// <summary>
    /// Gets connection statistics for all tracked IP addresses.
    /// </summary>
    /// <returns>Dictionary of IP addresses and their statistics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<string, (int Current, int Total)> GetAllConnections()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Pre-allocate dictionary with capacity to avoid resizing
        var result = new Dictionary<string, (int Current, int Total)>(
            _connectionInfo.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in _connectionInfo)
        {
            result[kvp.Key] = (kvp.Value.CurrentConnections, kvp.Value.TotalConnectionsToday);
        }

        return result;
    }

    /// <summary>
    /// Gets the total Number of concurrent connections across all IPs.
    /// </summary>
    /// <returns>The total connection count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetTotalConnectionCount()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int total = 0;
        foreach (var info in _connectionInfo.Values)
        {
            total += info.CurrentConnections;
        }
        _logger?.Debug("Total connections: {0}", total);

        return total;
    }

    /// <summary>
    /// Forcibly resets all connection counters.
    /// </summary>
    /// <remarks>
    /// This method is intended for use during system maintenance or after error recovery.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetAllCounters()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _connectionInfo.Clear();
        _logger?.Info("Counters reset");
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Cleans up stale connection records to prevent memory leaks.
    /// </summary>
    private async Task CleanupStaleConnectionsAsync()
    {
        if (_disposed) return;

        // Non-blocking attempt to acquire lock
        if (!await _cleanupLock.WaitAsync(0).ConfigureAwait(false))
            return;

        try
        {
            // Get current time once to avoid multiple calls
            DateTime now = DateTime.UtcNow;
            DateTime cutoffTime = now.Subtract(_inactivityThreshold);

            List<string>? keysToRemove = null;
            int processedCount = 0;

            // Process connections in batches for better performance
            foreach (var kvp in _connectionInfo)
            {
                // Limit the Number of entries processed in a single run
                if (processedCount >= MaxCleanupKeys)
                    break;

                var (key, info) = kvp;

                // Remove only if there are no active connections and it's been inactive
                if (info.CurrentConnections <= 0 && info.LastConnectionTime < cutoffTime)
                {
                    keysToRemove ??= new List<string>(Math.Min(EstimatedCollectionCapacity, _connectionInfo.Count));
                    keysToRemove.Add(key);

                    _logger?.Trace("Removed stale {0}", key);
                }

                processedCount++;
            }

            // Remove entries in batch
            if (keysToRemove != null)
            {
                foreach (string key in keysToRemove)
                    _connectionInfo.TryRemove(key, out _);
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            _logger?.Error("Cleanup error: {0}", ex.Message);
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    /// <summary>
    /// Creates a configured connection configuration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConnLimitConfig CreateConfiguredConfig(Action<ConnLimitConfig>? configure)
    {
        var config = ConfigurationStore.Instance.Get<ConnLimitConfig>();
        configure?.Invoke(config);
        return config;
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases all resources used by the <see cref="ConnectionLimiter"/> instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            _cleanupTimer?.Dispose();
            _cleanupLock?.Dispose();
            _connectionInfo.Clear();
        }
        catch (Exception ex)
        {
            _logger?.Error("Dispose error: {0}", ex.Message);
        }

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
