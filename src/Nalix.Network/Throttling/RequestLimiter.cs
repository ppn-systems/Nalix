// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Throttling.Metadata;
using Nalix.Shared.Configuration;
using Nalix.Shared.Injection;

namespace Nalix.Network.Throttling;

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

    private readonly RateLimitOptions _config;
    private readonly System.Int64 _timeWindowTicks;
    private readonly System.Int64 _lockoutDurationTicks;
    private readonly System.Threading.Timer _cleanupTimer;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.String, RequestLimiterInfo> _ipData;

    // Async fields
    private readonly System.Threading.Channels.Channel<CleanupRequest> _cleanupChannel;
    private readonly System.Threading.Channels.ChannelWriter<CleanupRequest> _cleanupWriter;
    private readonly System.Threading.Tasks.Task _cleanupTask;
    private readonly System.Threading.CancellationTokenSource _cancellationTokenSource;

    private System.Boolean _disposed;
    private System.Int32 _cleanupRunning;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLimiter"/> class with the provided firewall configuration and optional logger.
    /// </summary>
    /// <param name="config">The configuration for the firewall's rate-limiting settings. If <see langword="null"/>, the default configuration will be used.</param>
    /// <exception cref="InternalErrorException">
    /// Thrown when the configuration contains invalid rate-limiting settings.
    /// </exception>
    public RequestLimiter(RateLimitOptions? config = null)
    {
        this._config = config ?? ConfigurationManager.Instance.Get<RateLimitOptions>();

        ValidateConfiguration(this._config);

        this._ipData = new System.Collections.Concurrent.ConcurrentDictionary<System.String, RequestLimiterInfo>();
        this._timeWindowTicks = System.TimeSpan.FromMilliseconds(this._config.TimeWindowInMilliseconds).Ticks;
        this._lockoutDurationTicks = System.TimeSpan.FromSeconds(this._config.LockoutDurationSeconds).Ticks;

        // Initialize async components
        this._cancellationTokenSource = new System.Threading.CancellationTokenSource();

        var channelOptions = new System.Threading.Channels.BoundedChannelOptions(1000)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        this._cleanupChannel = System.Threading.Channels.Channel.CreateBounded<CleanupRequest>(channelOptions);
        this._cleanupWriter = this._cleanupChannel.Writer;

        // Activate background cleanup task
        this._cleanupTask = this.ProcessCleanupRequestsAsync(this._cancellationTokenSource.Token);

        // Keep original timer as fallback
        this._cleanupTimer = new System.Threading.Timer(static s
            => ((RequestLimiter)s!).TriggerCleanupAsync(), this, System.TimeSpan.FromMinutes(1), System.TimeSpan.FromMinutes(1));

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug(
                                    $"RequestLimiter initialized with async support - maxRequests={_config.MaxAllowedRequests}, " +
                                    $"timeWindow={_config.TimeWindowInMilliseconds}ms, lockout={_config.LockoutDurationSeconds}s");
    }

    /// <summary>
    /// Initializes with default configuration and logger.
    /// </summary>
    public RequestLimiter()
        : this((RateLimitOptions?)null)
    {
    }

    /// <summary>
    /// Initializes with custom configuration via action callback.
    /// </summary>
    public RequestLimiter(System.Action<RateLimitOptions>? configure = null)
        : this(CreateConfiguredConfig(configure))
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Synchronous version - for backward compatibility
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean CheckLimit([System.Diagnostics.CodeAnalysis.NotNull] System.String endPoint)
        => this.CheckLimitAsync(endPoint).AsTask().GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously checks the number of requests from an IP address and determines whether further requests are allowed.
    /// </summary>
    /// <param name="endPoint">The IP address to check the request limit for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the async operation with result: true if request is accepted, false if rejected.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask<System.Boolean> CheckLimitAsync(
        [System.Diagnostics.CodeAnalysis.NotNull]
        System.String endPoint,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, nameof(RequestLimiter));

        if (System.String.IsNullOrWhiteSpace(endPoint))
        {
            throw new InternalErrorException("EndPoint cannot be null or whitespace", nameof(endPoint));
        }

        // Fast path - synchronous check
        System.Int64 current = System.Diagnostics.Stopwatch.GetTimestamp();
        RequestLimiterInfo data = this._ipData.AddOrUpdate(
            endPoint,
            _ => new RequestLimiterInfo(current),
            (_, existing) => existing.Process(current, this._config.MaxAllowedRequests, this._timeWindowTicks, this._lockoutDurationTicks)
        );

        System.Boolean allowed = data.BlockedUntilTicks < current;

        // Trigger async cleanup if needed
        if (this.ShouldTriggerCleanup())
        {
            await this.TriggerCleanupAsync(cancellationToken).ConfigureAwait(false);
        }

        return allowed;
    }

    /// <summary>
    /// Bulk check for multiple endpoints - useful for batch processing
    /// </summary>
    public async System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<System.String, System.Boolean>> CheckLimitsAsync(
        System.Collections.Generic.IEnumerable<System.String> endPoints,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, nameof(RequestLimiter));

        System.Collections.Generic.Dictionary<
            System.String, System.Boolean> results;
        System.Collections.Generic.List<
            System.Threading.Tasks.Task<System.Collections.Generic.KeyValuePair<System.String, System.Boolean>>> tasks;

        tasks = [];
        results = [];

        foreach (var endPoint in endPoints)
        {
            var task = this.ProcessSingleEndPointAsync(endPoint, cancellationToken);
            tasks.Add(task);
        }

        System.Collections.Generic.KeyValuePair<System.String, System.Boolean>[] completedTasks =
            await System.Threading.Tasks.Task.WhenAll(tasks)
                                             .ConfigureAwait(false);

        foreach (System.Collections.Generic.KeyValuePair<System.String, System.Boolean> result in completedTasks)
        {
            results[result.Key] = result.Value;
        }

        return results;
    }

    /// <summary>
    /// Gets current statistics asynchronously
    /// </summary>
    public async System.Threading.Tasks.Task<RequestLimiterStatistics> GetStatisticsAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, nameof(RequestLimiter));

        await System.Threading.Tasks.Task.Yield(); // Make it properly async

        System.Int64 current = System.Diagnostics.Stopwatch.GetTimestamp();
        System.Int32 totalTrackedIPs = this._ipData.Count;
        System.Int32 blockedIPs = 0;
        System.Int32 activeRequests = 0;

        await System.Threading.Tasks.Task.Run(() =>
        {
            foreach (var info in this._ipData.Values)
            {
                if (info.BlockedUntilTicks >= current)
                {
                    _ = System.Threading.Interlocked.Increment(ref blockedIPs);
                }

                _ = System.Threading.Interlocked.Add(ref activeRequests, info.RequestCount);
            }
        }, cancellationToken).ConfigureAwait(false);

        return new RequestLimiterStatistics
        {
            TotalTrackedIPs = totalTrackedIPs,
            BlockedIPs = blockedIPs,
            ActiveRequests = activeRequests,
            Configuration = this._config
        };
    }

    /// <summary>
    /// Manually triggers cleanup operation
    /// </summary>
    public async System.Threading.Tasks.Task TriggerManualCleanupAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._disposed, nameof(RequestLimiter));

        await this.TriggerCleanupAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion Public Methods

    #region Private Async Methods

    /// <summary>
    /// Processes a single endpoint asynchronously
    /// </summary>
    private async System.Threading.Tasks.Task<
        System.Collections.Generic.KeyValuePair<System.String, System.Boolean>> ProcessSingleEndPointAsync(
        System.String endPoint,
        System.Threading.CancellationToken cancellationToken)
    {
        var result = await this.CheckLimitAsync(endPoint, cancellationToken).ConfigureAwait(false);
        return new System.Collections.Generic.KeyValuePair<System.String, System.Boolean>(endPoint, result);
    }

    /// <summary>
    /// Determines if cleanup should be triggered
    /// </summary>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean ShouldTriggerCleanup() =>
        // Trigger cleanup when we have too many tracked IPs
        this._ipData.Count > this._config.MaxAllowedRequests * 10;

    /// <summary>
    /// Triggers async cleanup
    /// </summary>
    private async System.Threading.Tasks.ValueTask TriggerCleanupAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        try
        {
            var cleanupRequest = new CleanupRequest(System.Diagnostics.Stopwatch.GetTimestamp());
            await this._cleanupWriter.WriteAsync(cleanupRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error("Failed to trigger async cleanup: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Timer callback for triggering async cleanup
    /// </summary>
    private async void TriggerCleanupAsync()
        => await this.TriggerCleanupAsync(this._cancellationTokenSource.Token);

    /// <summary>
    /// Background task for processing cleanup requests
    /// </summary>
    private async System.Threading.Tasks.Task ProcessCleanupRequestsAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        await foreach (var request in this._cleanupChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await this.PerformCleanupAsync(request.Timestamp, cancellationToken).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error("Async cleanup failed: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Performs the actual cleanup operation asynchronously
    /// </summary>
    private async System.Threading.Tasks.Task PerformCleanupAsync(
        System.Int64 currentTimestamp, System.Threading.CancellationToken cancellationToken)
    {
        if (this._disposed || System.Threading.Interlocked.Exchange(ref this._cleanupRunning, 1) == 1)
        {
            return;
        }

        try
        {
            await System.Threading.Tasks.Task.Yield(); // Ensure we're on background thread

            var toRemove = new System.Collections.Generic.List<System.String>();
            var processedCount = 0;
            const System.Int32 batchSize = 100;

            foreach (var kvp in this._ipData)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (ip, info) = kvp;
                info.Cleanup(currentTimestamp, this._timeWindowTicks);

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
                if (this._ipData.TryRemove(key, out _))
                {
                    removedCount++;
                }

                if (removedCount % batchSize == 0)
                {
                    await System.Threading.Tasks.Task.Yield();
                }
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("Async cleanup processed {0} IPs, removed {1} inactive IPs", processedCount, removedCount);
        }
        finally
        {
            _ = System.Threading.Interlocked.Exchange(ref this._cleanupRunning, 0);
        }
    }

    /// <summary>
    /// Validates the configuration parameters
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ValidateConfiguration(RateLimitOptions config)
    {
        if (config.MaxAllowedRequests <= 0)
        {
            throw new InternalErrorException("MaxAllowedRequests must be greater than 0");
        }

        if (config.LockoutDurationSeconds <= 0)
        {
            throw new InternalErrorException("LockoutDurationSeconds must be greater than 0");
        }

        if (config.TimeWindowInMilliseconds <= 0)
        {
            throw new InternalErrorException("TimeWindowInMilliseconds must be greater than 0");
        }
    }

    /// <summary>
    /// Creates a configured connection configuration.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static RateLimitOptions CreateConfiguredConfig(System.Action<RateLimitOptions>? configure)
    {
        var config = ConfigurationManager.Instance.Get<RateLimitOptions>();
        configure?.Invoke(config);
        return config;
    }

    #endregion Private Async Methods

    #region Helper Classes

    /// <summary>
    /// Request for cleanup operation
    /// </summary>
    private readonly record struct CleanupRequest(System.Int64 Timestamp);

    /// <summary>
    /// Represents statistics related to the request limiter system,
    /// including tracking of IPs and current request activity.
    /// </summary>
    public readonly record struct RequestLimiterStatistics
    {
        /// <summary>
        /// Gets the total number of IP addresses currently being tracked.
        /// </summary>
        public System.Int32 TotalTrackedIPs { get; init; }

        /// <summary>
        /// Gets the number of IP addresses that are currently blocked due to rate limits.
        /// </summary>
        public System.Int32 BlockedIPs { get; init; }

        /// <summary>
        /// Gets the number of active ongoing requests being handled at this moment.
        /// </summary>
        public System.Int32 ActiveRequests { get; init; }

        /// <summary>
        /// Gets the configuration used for rate limiting evaluation.
        /// </summary>
        public RateLimitOptions Configuration { get; init; }
    }


    #endregion Helper Classes

    #region IDisposable & IAsyncDisposable

    /// <inheritdoc />
    public void Dispose() => this.DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;

        // Cancel all operations
        this._cancellationTokenSource.Cancel();

        // Complete the cleanup channel
        this._cleanupWriter.Complete();

        try
        {
            // Wait for cleanup task to complete
            await this._cleanupTask.WaitAsync(System.TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (System.TimeoutException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn("Cleanup task did not complete within timeout");
        }

        // Dispose resources
        this._cleanupTimer.Dispose();
        this._cancellationTokenSource.Dispose();
        this._ipData.Clear();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug("RequestLimiter disposed asynchronously");
    }

    #endregion IDisposable & IAsyncDisposable
}