// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Options;

namespace Nalix.Logging.Core;

/// <summary>
/// Provides health check monitoring for the logging system.
/// </summary>
/// <remarks>
/// This class monitors logging system health by tracking throughput, error rates,
/// and queue depths over a sliding time window.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("Status={Status}, Throughput={ThroughputPerSecond:F1}/s, ErrorRate={ErrorRatePercent:F1}%")]
public sealed class LoggingHealthCheck : ILoggingHealthCheck, System.IDisposable
{
    #region Fields

    private readonly HealthCheckOptions _options;
    private readonly NLogixDistributor _distributor;
    private readonly System.Collections.Concurrent.ConcurrentQueue<MetricSnapshot> _metrics;
    private readonly System.Threading.Timer? _checkTimer;

    private System.Int32 _isDisposed;
    private System.Int32 _currentStatus; // HealthStatus as Int32
    private System.DateTime _startTime;

    #endregion Fields

    #region Properties

    /// <inheritdoc/>
    public HealthStatus Status
    {
        get => (HealthStatus)System.Threading.Volatile.Read(ref _currentStatus);
        private set => System.Threading.Volatile.Write(ref _currentStatus, (System.Int32)value);
    }

    /// <inheritdoc/>
    public System.Double ThroughputPerSecond { get; private set; }

    /// <inheritdoc/>
    public System.Double ErrorRatePercent { get; private set; }

    /// <inheritdoc/>
    public System.Int32 CurrentQueueDepth { get; private set; }

    /// <inheritdoc/>
    public System.Int64 TotalEntriesProcessed => _distributor.TotalEntriesPublished;

    /// <inheritdoc/>
    public System.Int64 TotalErrors => _distributor.TotalPublishErrors;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingHealthCheck"/> class.
    /// </summary>
    /// <param name="distributor">The log distributor to monitor.</param>
    /// <param name="options">Health check configuration options.</param>
    public LoggingHealthCheck(NLogixDistributor distributor, HealthCheckOptions? options = null)
    {
        System.ArgumentNullException.ThrowIfNull(distributor);

        _distributor = distributor;
        _options = options ?? new HealthCheckOptions();
        _options.Validate();

        _metrics = new System.Collections.Concurrent.ConcurrentQueue<MetricSnapshot>();
        _startTime = System.DateTime.UtcNow;
        _currentStatus = (System.Int32)HealthStatus.Healthy;

        // Start periodic health checks if enabled
        if (_options.Enabled)
        {
            _checkTimer = new System.Threading.Timer(
                PeriodicHealthCheck,
                null,
                _options.CheckInterval,
                _options.CheckInterval);
        }
    }

    #endregion Constructors

    #region ILoggingHealthCheck Implementation

    /// <inheritdoc/>
    public HealthStatus CheckHealth()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(LoggingHealthCheck));

        if (!_options.Enabled)
        {
            return HealthStatus.Healthy;
        }

        // Capture current metrics
        var snapshot = new MetricSnapshot
        {
            Timestamp = System.DateTime.UtcNow,
            EntriesProcessed = TotalEntriesProcessed,
            Errors = TotalErrors,
            QueueDepth = 0 // Queue depth tracking requires access to internal provider state
        };

        _metrics.Enqueue(snapshot);

        // Clean up old metrics outside the window
        CleanupOldMetrics();

        // Calculate metrics over the window
        CalculateMetrics();

        // Determine health status
        var newStatus = DetermineHealthStatus();
        Status = newStatus;

        if (_options.LogHealthCheckResults)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Health Check: {newStatus}, " +
                $"Throughput: {ThroughputPerSecond:F1}/s, Error Rate: {ErrorRatePercent:F1}%");
        }

        return newStatus;
    }

    /// <inheritdoc/>
    public System.String GetDiagnostics()
    {
        var uptime = System.DateTime.UtcNow - _startTime;
        var sb = new System.Text.StringBuilder();

        _ = sb.AppendLine($"[Logging Health Status - {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC]");
        _ = sb.AppendLine($"Status: {Status}");
        _ = sb.AppendLine($"Uptime: {uptime.TotalHours:N1} hours");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Current Metrics:");
        _ = sb.AppendLine($"  Throughput: {ThroughputPerSecond:N1} entries/second");
        _ = sb.AppendLine($"  Error Rate: {ErrorRatePercent:N2}%");
        _ = sb.AppendLine($"  Queue Depth: {CurrentQueueDepth:N0}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Lifetime Statistics:");
        _ = sb.AppendLine($"  Total Entries: {TotalEntriesProcessed:N0}");
        _ = sb.AppendLine($"  Total Errors: {TotalErrors:N0}");
        _ = sb.AppendLine($"  Total Target Invocations: {_distributor.TotalTargetInvocations:N0}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Thresholds:");
        _ = sb.AppendLine($"  Max Error Rate: {_options.MaxErrorRatePercent:N1}%");
        _ = sb.AppendLine($"  Max Queue Depth: {_options.MaxQueueDepth:N0}");
        _ = sb.AppendLine($"  Critical Queue Depth: {_options.CriticalQueueDepth:N0}");

        if (_options.MinThroughputPerSecond > 0)
        {
            _ = sb.AppendLine($"  Min Throughput: {_options.MinThroughputPerSecond:N0}/s");
        }

        if (_options.IncludeDetailedDiagnostics)
        {
            _ = sb.AppendLine();
            _ = sb.AppendLine("Detailed Information:");
            _ = sb.AppendLine($"  Metrics Window: {_options.MetricsWindow.TotalSeconds:N0} seconds");
            _ = sb.AppendLine($"  Check Interval: {_options.CheckInterval.TotalSeconds:N0} seconds");
            _ = sb.AppendLine($"  Metric Samples: {_metrics.Count}");
        }

        return sb.ToString();
    }

    #endregion ILoggingHealthCheck Implementation

    #region IDisposable Implementation

    /// <summary>
    /// Releases resources used by the health check monitor.
    /// </summary>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _checkTimer?.Dispose();
        _metrics.Clear();

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable Implementation

    #region Private Methods

    private void PeriodicHealthCheck(System.Object? state)
    {
        try
        {
            _ = CheckHealth();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error in periodic health check: {ex.Message}");
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CleanupOldMetrics()
    {
        var cutoff = System.DateTime.UtcNow - _options.MetricsWindow;

        while (_metrics.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            _ = _metrics.TryDequeue(out _);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CalculateMetrics()
    {
        if (_metrics.IsEmpty)
        {
            ThroughputPerSecond = 0;
            ErrorRatePercent = 0;
            CurrentQueueDepth = 0;
            return;
        }

        var snapshots = _metrics.ToArray();
        if (snapshots.Length < 2)
        {
            ThroughputPerSecond = 0;
            ErrorRatePercent = 0;
            CurrentQueueDepth = snapshots.Length > 0 ? snapshots[0].QueueDepth : 0;
            return;
        }

        var oldest = snapshots[0];
        var newest = snapshots[^1];

        var timeSpan = (newest.Timestamp - oldest.Timestamp).TotalSeconds;
        if (timeSpan > 0)
        {
            var entriesDelta = newest.EntriesProcessed - oldest.EntriesProcessed;
            ThroughputPerSecond = entriesDelta / timeSpan;

            var errorsDelta = newest.Errors - oldest.Errors;
            ErrorRatePercent = entriesDelta > 0
                ? (errorsDelta / (System.Double)entriesDelta) * 100.0
                : 0;
        }

        CurrentQueueDepth = newest.QueueDepth;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private HealthStatus DetermineHealthStatus()
    {
        // Check critical conditions first
        if (CurrentQueueDepth >= _options.CriticalQueueDepth)
        {
            return HealthStatus.Unhealthy;
        }

        // Check degraded conditions
        var isDegraded = false;

        if (ErrorRatePercent > _options.MaxErrorRatePercent)
        {
            isDegraded = true;
        }

        if (CurrentQueueDepth >= _options.MaxQueueDepth)
        {
            isDegraded = true;
        }

        if (_options.MinThroughputPerSecond > 0 &&
            ThroughputPerSecond < _options.MinThroughputPerSecond)
        {
            isDegraded = true;
        }

        return isDegraded ? HealthStatus.Degraded : HealthStatus.Healthy;
    }

    #endregion Private Methods

    #region Nested Types

    private sealed class MetricSnapshot
    {
        public System.DateTime Timestamp { get; init; }
        public System.Int64 EntriesProcessed { get; init; }
        public System.Int64 Errors { get; init; }
        public System.Int32 QueueDepth { get; init; }
    }

    #endregion Nested Types
}
