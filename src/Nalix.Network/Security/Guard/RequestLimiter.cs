using Nalix.Common.Exceptions;
using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Security.Metadata;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Security.Guard;

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
public sealed class RequestLimiter : System.IDisposable, System.IAsyncDisposable
{
    #region Fields

    private readonly ILogger? _logger;
    private readonly System.Threading.Timer _cleanupTimer;
    private readonly RequestRateLimitOptions _config;
    private readonly long _timeWindowTicks;
    private readonly long _lockoutDurationTicks;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RequestLimiterInfo> _ipData;

    // Async fields
    private readonly System.Threading.Channels.Channel<CleanupRequest> _cleanupChannel;
    private readonly System.Threading.Channels.ChannelWriter<CleanupRequest> _cleanupWriter;
    private readonly System.Threading.Tasks.Task _cleanupTask;
    private readonly System.Threading.CancellationTokenSource _cancellationTokenSource;

    private bool _disposed;
    private int _cleanupRunning;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLimiter"/> class with the provided firewall configuration and optional logger.
    /// </summary>
    /// <param name="config">The configuration for the firewall's rate-limiting settings. If <see langword="null"/>, the default configuration will be used.</param>
    /// <param name="logger">An optional logger for logging purposes. If <see langword="null"/>, no logging will be done.</param>
    /// <exception cref="InternalErrorException">
    /// Thrown when the configuration contains invalid rate-limiting settings.
    /// </exception>
    public RequestLimiter(RequestRateLimitOptions? config = null, ILogger? logger = null)
    {
        _logger = logger;
        _config = config ?? ConfigurationStore.Instance.Get<RequestRateLimitOptions>();

        ValidateConfiguration(_config);

        _ipData = new System.Collections.Concurrent.ConcurrentDictionary<string, RequestLimiterInfo>();
        _timeWindowTicks = System.TimeSpan.FromMilliseconds(_config.TimeWindowInMilliseconds).Ticks;
        _lockoutDurationTicks = System.TimeSpan.FromSeconds(_config.LockoutDurationSeconds).Ticks;

        // Initialize async components
        _cancellationTokenSource = new System.Threading.CancellationTokenSource();

        var channelOptions = new System.Threading.Channels.BoundedChannelOptions(1000)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _cleanupChannel = System.Threading.Channels.Channel.CreateBounded<CleanupRequest>(channelOptions);
        _cleanupWriter = _cleanupChannel.Writer;

        // Start background cleanup task
        _cleanupTask = ProcessCleanupRequestsAsync(_cancellationTokenSource.Token);

        // Keep original timer as fallback
        _cleanupTimer = new System.Threading.Timer(static s
            => ((RequestLimiter)s!).TriggerCleanupAsync(), this, System.TimeSpan.FromMinutes(1), System.TimeSpan.FromMinutes(1));

        _logger?.Debug("RequestLimiter initialized with async support - maxRequests={0}, timeWindow={1}ms, lockout={2}s",
            _config.MaxAllowedRequests, _config.TimeWindowInMilliseconds, _config.LockoutDurationSeconds);
    }

    /// <summary>
    /// Initializes with default configuration and logger.
    /// </summary>
    public RequestLimiter(ILogger? logger = null)
        : this((RequestRateLimitOptions?)null, logger)
    {
    }

    /// <summary>
    /// Initializes with custom configuration via action callback.
    /// </summary>
    public RequestLimiter(System.Action<RequestRateLimitOptions>? configure = null, ILogger? logger = null)
        : this(CreateConfiguredConfig(configure), logger)
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Synchronous version - for backward compatibility
    /// </summary>
    public bool CheckLimit([System.Diagnostics.CodeAnalysis.NotNull] string endPoint)
    {
        return CheckLimitAsync(endPoint).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously checks the number of requests from an IP address and determines whether further requests are allowed.
    /// </summary>
    /// <param name="endPoint">The IP address to check the request limit for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the async operation with result: true if request is accepted, false if rejected.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask<bool> CheckLimitAsync(
        [System.Diagnostics.CodeAnalysis.NotNull]
        string endPoint,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(RequestLimiter));

        if (string.IsNullOrWhiteSpace(endPoint))
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));

        // Fast path - synchronous check
        long current = System.Diagnostics.Stopwatch.GetTimestamp();
        var data = _ipData.AddOrUpdate(
            endPoint,
            _ => new RequestLimiterInfo(current),
            (_, existing) => existing.Process(current, _config.MaxAllowedRequests, _timeWindowTicks, _lockoutDurationTicks)
        );

        bool allowed = data.BlockedUntilTicks < current;

        // Async logging to avoid blocking
        if (_logger is not null)
        {
            _ = System.Threading.Tasks.Task.Run(() => LogRequestResultAsync(endPoint, allowed, data.LastRequestTicks), cancellationToken);
        }

        // Trigger async cleanup if needed
        if (ShouldTriggerCleanup())
        {
            await TriggerCleanupAsync(cancellationToken).ConfigureAwait(false);
        }

        return allowed;
    }

    /// <summary>
    /// Bulk check for multiple endpoints - useful for batch processing
    /// </summary>
    public async System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<string, bool>> CheckLimitsAsync(
        System.Collections.Generic.IEnumerable<string> endPoints,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(RequestLimiter));

        var results = new System.Collections.Generic.Dictionary<string, bool>();
        var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task<System.Collections.Generic.KeyValuePair<string, bool>>>();

        foreach (var endPoint in endPoints)
        {
            var task = ProcessSingleEndPointAsync(endPoint, cancellationToken);
            tasks.Add(task);
        }

        var completedTasks = await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in completedTasks)
        {
            results[result.Key] = result.Value;
        }

        return results;
    }

    /// <summary>
    /// Gets current statistics asynchronously
    /// </summary>
    public async System.Threading.Tasks.Task<RequestLimiterStatistics> GetStatisticsAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(RequestLimiter));

        await System.Threading.Tasks.Task.Yield(); // Make it properly async

        long current = System.Diagnostics.Stopwatch.GetTimestamp();
        int totalTrackedIPs = _ipData.Count;
        int blockedIPs = 0;
        int activeRequests = 0;

        await System.Threading.Tasks.Task.Run(() =>
        {
            foreach (var info in _ipData.Values)
            {
                if (info.BlockedUntilTicks >= current)
                    System.Threading.Interlocked.Increment(ref blockedIPs);

                System.Threading.Interlocked.Add(ref activeRequests, info.RequestCount);
            }
        }, cancellationToken).ConfigureAwait(false);

        return new RequestLimiterStatistics
        {
            TotalTrackedIPs = totalTrackedIPs,
            BlockedIPs = blockedIPs,
            ActiveRequests = activeRequests,
            Configuration = _config
        };
    }

    /// <summary>
    /// Manually triggers cleanup operation
    /// </summary>
    public async System.Threading.Tasks.Task TriggerManualCleanupAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(RequestLimiter));

        await TriggerCleanupAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion Public Methods

    #region Private Async Methods

    /// <summary>
    /// Processes a single endpoint asynchronously
    /// </summary>
    private async System.Threading.Tasks.Task<System.Collections.Generic.KeyValuePair<string, bool>> ProcessSingleEndPointAsync(
        string endPoint,
        System.Threading.CancellationToken cancellationToken)
    {
        var result = await CheckLimitAsync(endPoint, cancellationToken).ConfigureAwait(false);
        return new System.Collections.Generic.KeyValuePair<string, bool>(endPoint, result);
    }

    /// <summary>
    /// Async logging to avoid blocking main thread
    /// </summary>
    private async System.Threading.Tasks.Task LogRequestResultAsync(string endPoint, bool allowed, long lastRequestTicks)
    {
        try
        {
            await System.Threading.Tasks.Task.Yield(); // Ensure we're on background thread

            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(lastRequestTicks);

            if (allowed)
            {
                _logger?.Debug("Request from {0} allowed, elapsed: {1:g}", endPoint, elapsed);
            }
            else
            {
                _logger?.Warn("Request from {0} blocked, elapsed: {1:g}", endPoint, elapsed);
            }
        }
        catch (System.Exception ex)
        {
            _logger?.Error("Async logging failed: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Determines if cleanup should be triggered
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool ShouldTriggerCleanup()
    {
        // Trigger cleanup when we have too many tracked IPs
        return _ipData.Count > _config.MaxAllowedRequests * 10;
    }

    /// <summary>
    /// Triggers async cleanup
    /// </summary>
    private async System.Threading.Tasks.ValueTask TriggerCleanupAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        try
        {
            var cleanupRequest = new CleanupRequest(System.Diagnostics.Stopwatch.GetTimestamp());
            await _cleanupWriter.WriteAsync(cleanupRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (System.Exception ex)
        {
            _logger?.Error("Failed to trigger async cleanup: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Timer callback for triggering async cleanup
    /// </summary>
    private async void TriggerCleanupAsync()
    {
        await TriggerCleanupAsync(_cancellationTokenSource.Token);
    }

    /// <summary>
    /// Background task for processing cleanup requests
    /// </summary>
    private async System.Threading.Tasks.Task ProcessCleanupRequestsAsync(System.Threading.CancellationToken cancellationToken)
    {
        await foreach (var request in _cleanupChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await PerformCleanupAsync(request.Timestamp, cancellationToken).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
            {
                _logger?.Error("Async cleanup failed: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Performs the actual cleanup operation asynchronously
    /// </summary>
    private async System.Threading.Tasks.Task PerformCleanupAsync(long currentTimestamp, System.Threading.CancellationToken cancellationToken)
    {
        if (_disposed || System.Threading.Interlocked.Exchange(ref _cleanupRunning, 1) == 1)
            return;

        try
        {
            await System.Threading.Tasks.Task.Yield(); // Ensure we're on background thread

            var toRemove = new System.Collections.Generic.List<string>();
            var processedCount = 0;
            const int batchSize = 100;

            foreach (var kvp in _ipData)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (ip, info) = kvp;
                info.Cleanup(currentTimestamp, _timeWindowTicks);

                if (info.RequestCount == 0 && info.BlockedUntilTicks < currentTimestamp)
                {
                    toRemove.Add(ip);
                }

                processedCount++;

                // Yield control periodically for large datasets
                if (processedCount % batchSize == 0)
                {
                    await System.Threading.Tasks.Task.Yield();
                }
            }

            // Remove inactive IPs in batches
            var removedCount = 0;
            foreach (var key in toRemove)
            {
                if (_ipData.TryRemove(key, out _))
                {
                    removedCount++;
                }

                if (removedCount % batchSize == 0)
                {
                    await System.Threading.Tasks.Task.Yield();
                }
            }

            _logger?.Debug("Async cleanup processed {0} IPs, removed {1} inactive IPs",
                          processedCount, removedCount);
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _cleanupRunning, 0);
        }
    }

    /// <summary>
    /// Validates the configuration parameters
    /// </summary>
    private static void ValidateConfiguration(RequestRateLimitOptions config)
    {
        if (config.MaxAllowedRequests <= 0)
            throw new InternalErrorException("MaxAllowedRequests must be greater than 0");
        if (config.LockoutDurationSeconds <= 0)
            throw new InternalErrorException("LockoutDurationSeconds must be greater than 0");
        if (config.TimeWindowInMilliseconds <= 0)
            throw new InternalErrorException("TimeWindowInMilliseconds must be greater than 0");
    }

    /// <summary>
    /// Creates a configured connection configuration.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static RequestRateLimitOptions CreateConfiguredConfig(System.Action<RequestRateLimitOptions>? configure)
    {
        var config = ConfigurationStore.Instance.Get<RequestRateLimitOptions>();
        configure?.Invoke(config);
        return config;
    }

    #endregion Private Async Methods

    #region Helper Classes

    /// <summary>
    /// Request for cleanup operation
    /// </summary>
    private readonly record struct CleanupRequest(long Timestamp);

    /// <summary>
    /// Represents statistics related to the request limiter system,
    /// including tracking of IPs and current request activity.
    /// </summary>
    public readonly record struct RequestLimiterStatistics
    {
        /// <summary>
        /// Gets the total number of IP addresses currently being tracked.
        /// </summary>
        public int TotalTrackedIPs { get; init; }

        /// <summary>
        /// Gets the number of IP addresses that are currently blocked due to rate limits.
        /// </summary>
        public int BlockedIPs { get; init; }

        /// <summary>
        /// Gets the number of active ongoing requests being handled at this moment.
        /// </summary>
        public int ActiveRequests { get; init; }

        /// <summary>
        /// Gets the configuration used for rate limiting evaluation.
        /// </summary>
        public RequestRateLimitOptions Configuration { get; init; }
    }


    #endregion Helper Classes

    #region IDisposable & IAsyncDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        // Cancel all operations
        _cancellationTokenSource.Cancel();

        // Complete the cleanup channel
        _cleanupWriter.Complete();

        try
        {
            // Wait for cleanup task to complete
            await _cleanupTask.WaitAsync(System.TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (System.TimeoutException)
        {
            _logger?.Warn("Cleanup task did not complete within timeout");
        }

        // Dispose resources
        _cleanupTimer.Dispose();
        _cancellationTokenSource.Dispose();
        _ipData.Clear();

        _logger?.Debug("RequestLimiter disposed asynchronously");
    }

    #endregion IDisposable & IAsyncDisposable
}