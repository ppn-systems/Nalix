using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Throttling.Metadata;
using Nalix.Shared.Configuration;
using Nalix.Shared.Injection;

namespace Nalix.Network.Throttling;

/// <summary>
/// A high-performance connection limiter that restricts simultaneous connections from IP addresses
/// to prevent abuse and resource exhaustion.
/// </summary>
public sealed class ConnectionLimiter : System.IDisposable
{
    #region Constants

    // LZ4CompressionConstants for optimization
    private const System.Int32 MaxCleanupKeys = 1000;

    private const System.Int32 EstimatedCollectionCapacity = 256;

    #endregion Constants

    #region Fields

    private static readonly System.DateTime DateTimeUnixEpoch = new(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

    private readonly ConnLimitOptions _config;

    private readonly System.Threading.Timer _cleanupTimer;
    private readonly System.Threading.SemaphoreSlim _cleanupLock;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Net.IPAddress, ConnectionLimitInfo> _connectionInfo;

    // Cache frequently accessed configuration values
    private readonly System.Int32 _maxConnectionsPerIp;

    private readonly System.TimeSpan _inactivityThreshold;

    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionLimiter"/> class.
    /// </summary>
    /// <param name="connectionConfig">The connection configuration. If null, default config is used.</param>
    /// <exception cref="System.ArgumentException">Thrown when configuration has invalid values.</exception>
    public ConnectionLimiter(ConnLimitOptions? connectionConfig = null)
    {
        this._config = connectionConfig ?? ConfigurationManager.Instance.Get<ConnLimitOptions>();

        if (this._config.MaxConnectionsPerIpAddress <= 0)
        {
            throw new System.ArgumentException("MaxConnectionsPerIpAddress must be greater than 0",
                nameof(connectionConfig));
        }

        // Cache configuration values for performance
        this._maxConnectionsPerIp = this._config.MaxConnectionsPerIpAddress;
        this._inactivityThreshold = this._config.InactivityThreshold;

        // Initialize with case-insensitive string comparer for IP addresses
        this._cleanupLock = new System.Threading.SemaphoreSlim(1, 1);
        this._connectionInfo = new System.Collections.Concurrent.ConcurrentDictionary<
            System.Net.IPAddress, ConnectionLimitInfo>();

        // StartTickLoopAsync cleanup timer with configured interval
        this._cleanupTimer = new System.Threading.Timer(
            async _ => await this.CleanupStaleConnectionsAsync().ConfigureAwait(false),
            null,
            this._config.CleanupInterval,
            this._config.CleanupInterval
        );

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"ConnectionLimiter initialized: " +
                                       $"max={_maxConnectionsPerIp}, inactivity={_inactivityThreshold.TotalSeconds}s");
    }

    /// <summary>
    /// Initializes with custom configuration via action callback.
    /// </summary>
    public ConnectionLimiter(System.Action<ConnLimitOptions>? configure = null)
        : this(CreateConfiguredConfig(configure))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionLimiter"/> class with default configuration and logger.
    /// </summary>
    public ConnectionLimiter()
        : this((ConnLimitOptions?)null)
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Determines whether a new connection is allowed for the specified IP address.
    /// </summary>
    /// <param name="endPoint">The IP address or endpoint to check.</param>
    /// <returns><c>true</c> if the connection is allowed; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ArgumentException">Thrown if endpoint is null or empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsConnectionAllowed(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress endPoint)
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, this);

        if (endPoint is null)
        {
            throw new System.ArgumentException("EndPoint cannot be null", nameof(endPoint));
        }

        System.DateTime now = System.DateTime.UtcNow;
        System.DateTime currentDate = now.Date;

        // CheckLimit if endpoint already exists
        if (this._connectionInfo.TryGetValue(endPoint, out var existingInfo))
        {
            // Fast path for already at limit
            if (existingInfo.CurrentConnections >= this._maxConnectionsPerIp)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"Limit exceeded for {endPoint}: " +
                                               $"{existingInfo.CurrentConnections}/{this._maxConnectionsPerIp}");

                return false;
            }

            // Fast path for typical case
            System.Int32 totalToday = currentDate > existingInfo.LastConnectionTime.Date ?
                1 : existingInfo.TotalConnectionsToday + 1;

            ConnectionLimitInfo newInfo = existingInfo with
            {
                CurrentConnections = existingInfo.CurrentConnections + 1,
                LastConnectionTime = now,
                TotalConnectionsToday = totalToday
            };

            this._connectionInfo[endPoint] = newInfo;
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace("Allowed {0}", endPoint);

            return true;
        }

        // New endpoint
        ConnectionLimitInfo info = new(1, now, 1, now);
        this._connectionInfo[endPoint] = info;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace("Allowed {0}", endPoint);

        return true;
    }

    /// <summary>
    /// Marks a connection as closed for the specified IP address.
    /// </summary>
    /// <param name="endPoint">The IP address or endpoint.</param>
    /// <returns>True if successfully marked as closed.</returns>
    /// <exception cref="System.ArgumentException">Thrown if endpoint is null or empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean ConnectionClosed(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress endPoint)
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, this);

        if (endPoint is null)
        {
            throw new System.ArgumentException("EndPoint cannot be null", nameof(endPoint));
        }

        // Fast path if entry doesn't exist
        if (!this._connectionInfo.TryGetValue(endPoint, out ConnectionLimitInfo existingInfo))
        {
            return false;
        }

        // SynchronizeTime the current connections count
        ConnectionLimitInfo newInfo = existingInfo with
        {
            CurrentConnections = System.Math.Max(0, existingInfo.CurrentConnections - 1),
            LastConnectionTime = System.DateTime.UtcNow
        };

        this._connectionInfo[endPoint] = newInfo;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace("Closed {0}", endPoint);

        return true;
    }

    /// <summary>
    /// Gets connection information for the specified IP address.
    /// </summary>
    /// <param name="endPoint">The IP address or endpoint.</param>
    /// <returns>Connection statistics tuple.</returns>
    /// <exception cref="System.ArgumentException">Thrown if endpoint is null or empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (System.Int32 CurrentConnections, System.Int32 TotalToday, System.DateTime LastConnection) GetConnectionInfo(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress endPoint)
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, this);

        if (endPoint is null)
        {
            throw new System.ArgumentException("EndPoint cannot be null", nameof(endPoint));
        }

        if (this._connectionInfo.TryGetValue(endPoint, out var stats))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"Observability for {endPoint}: " +
                                           $"{stats.CurrentConnections} current, {stats.TotalConnectionsToday} today");
            return (stats.CurrentConnections, stats.TotalConnectionsToday, stats.LastConnectionTime);
        }

        return (0, 0, DateTimeUnixEpoch);
    }

    /// <summary>
    /// Gets connection statistics for all tracked IP addresses.
    /// </summary>
    /// <returns>Dictionary of IP addresses and their statistics.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.Dictionary<
        System.Net.IPAddress, (System.Int32 Current, System.Int32 Total)> GetAllConnections()
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, this);

        // PreDispatch-allocate dictionary with capacity to avoid resizing
        System.Collections.Generic.Dictionary<
            System.Net.IPAddress, (System.Int32 Current, System.Int32 Total)> result = new(this._connectionInfo.Count);

        foreach (var kvp in this._connectionInfo)
        {
            result[kvp.Key] = (kvp.Value.CurrentConnections, kvp.Value.TotalConnectionsToday);
        }

        return result;
    }

    /// <summary>
    /// Gets the total TransportProtocol of concurrent connections across all IPs.
    /// </summary>
    /// <returns>The total connection count.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Int32 GetTotalConnectionCount()
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, this);

        System.Int32 total = 0;
        foreach (var info in this._connectionInfo.Values)
        {
            total += info.CurrentConnections;
        }
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug("Total connections: {0}", total);

        return total;
    }

    /// <summary>
    /// Forcibly resets all connection counters.
    /// </summary>
    /// <remarks>
    /// This method is intended for use during system maintenance or after error recovery.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ResetAllCounters()
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, this);

        this._connectionInfo.Clear();
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info("Counters reset");
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Cleans up stale connection records to prevent memory leaks.
    /// </summary>
    private async System.Threading.Tasks.Task CleanupStaleConnectionsAsync()
    {
        if (this._disposed)
        {
            return;
        }

        // Non-blocking attempt to acquire lock
        if (!await this._cleanupLock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            // Get current time once to avoid multiple calls
            System.DateTime now = System.DateTime.UtcNow;
            System.DateTime cutoffTime = now.Subtract(this._inactivityThreshold);

            System.Collections.Generic.List<System.Net.IPAddress>? keysToRemove = null;
            System.Int32 processedCount = 0;

            // Process connections in batches for better performance
            foreach (var kvp in this._connectionInfo)
            {
                // Limit the TransportProtocol of entries processed in a single run
                if (processedCount >= MaxCleanupKeys)
                {
                    break;
                }

                var (key, info) = kvp;

                // Remove only if there are no active connections and it's been inactive
                if (info.CurrentConnections <= 0 && info.LastConnectionTime < cutoffTime)
                {
                    keysToRemove ??= new System.Collections.Generic.List<System.Net.IPAddress>(
                        System.Math.Min(EstimatedCollectionCapacity, this._connectionInfo.Count));

                    keysToRemove.Add(key);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace("Removed stale {0}", key);
                }

                processedCount++;
            }

            // Remove entries in batch
            if (keysToRemove != null)
            {
                foreach (System.Net.IPAddress key in keysToRemove)
                {
                    _ = this._connectionInfo.TryRemove(key, out _);
                }
            }
        }
        catch (System.Exception ex) when (ex is not System.ObjectDisposedException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error("Cleanup error: {0}", ex.Message);
        }
        finally
        {
            _ = this._cleanupLock.Release();
        }
    }

    /// <summary>
    /// Creates a configured connection configuration.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ConnLimitOptions CreateConfiguredConfig(
        System.Action<ConnLimitOptions>? configure)
    {
        var config = ConfigurationManager.Instance.Get<ConnLimitOptions>();
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
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;

        try
        {
            this._connectionInfo.Clear();

            this._cleanupLock?.Dispose();
            this._cleanupTimer?.Dispose();
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error("Dispose error: {0}", ex.Message);
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}