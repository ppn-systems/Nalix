// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Options;

/// <summary>
/// Configuration options for logging health check monitoring.
/// </summary>
/// <remarks>
/// Health checks monitor the logging system's health status, including throughput,
/// error rates, and queue depths to detect issues early.
/// </remarks>
public sealed class HealthCheckOptions
{
    #region Properties

    /// <summary>
    /// Gets or sets whether health checking is enabled.
    /// </summary>
    /// <remarks>
    /// Default is true. When disabled, no health metrics are collected.
    /// </remarks>
    public System.Boolean Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval at which health metrics are collected.
    /// </summary>
    /// <remarks>
    /// Default is 30 seconds. Must be greater than TimeSpan.Zero.
    /// </remarks>
    public System.TimeSpan CheckInterval { get; set; } = System.TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the time window for calculating throughput and error rates.
    /// </summary>
    /// <remarks>
    /// Default is 60 seconds. Metrics are calculated over this sliding window.
    /// Must be greater than TimeSpan.Zero.
    /// </remarks>
    public System.TimeSpan MetricsWindow { get; set; } = System.TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the maximum acceptable error rate (as a percentage 0-100).
    /// </summary>
    /// <remarks>
    /// Default is 5%. Health status becomes Degraded when error rate exceeds this threshold.
    /// Must be between 0 and 100.
    /// </remarks>
    public System.Double MaxErrorRatePercent { get; set; } = 5.0;

    /// <summary>
    /// Gets or sets the maximum acceptable queue depth before health becomes degraded.
    /// </summary>
    /// <remarks>
    /// Default is 1000 entries. When queue exceeds this, health status becomes Degraded.
    /// Must be greater than 0.
    /// </remarks>
    public System.Int32 MaxQueueDepth { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the critical queue depth threshold before health becomes unhealthy.
    /// </summary>
    /// <remarks>
    /// Default is 5000 entries. When queue exceeds this, health status becomes Unhealthy.
    /// Must be greater than <see cref="MaxQueueDepth"/>.
    /// </remarks>
    public System.Int32 CriticalQueueDepth { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the minimum acceptable throughput (entries per second).
    /// </summary>
    /// <remarks>
    /// Default is 0 (no minimum). When throughput falls below this, health becomes Degraded.
    /// Set to 0 to disable throughput checking.
    /// </remarks>
    public System.Int32 MinThroughputPerSecond { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to include detailed diagnostics in health reports.
    /// </summary>
    /// <remarks>
    /// Default is true. When enabled, health reports include detailed metrics and diagnostics.
    /// </remarks>
    public System.Boolean IncludeDetailedDiagnostics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log health check results.
    /// </summary>
    /// <remarks>
    /// Default is false. When enabled, health check results are logged to Debug output.
    /// Be cautious enabling this to avoid recursive logging.
    /// </remarks>
    public System.Boolean LogHealthCheckResults { get; set; } = false;

    #endregion Properties

    #region Methods

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="System.ArgumentException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (CheckInterval <= System.TimeSpan.Zero)
        {
            throw new System.ArgumentException(
                $"{nameof(CheckInterval)} must be greater than TimeSpan.Zero.", nameof(CheckInterval));
        }

        if (MetricsWindow <= System.TimeSpan.Zero)
        {
            throw new System.ArgumentException(
                $"{nameof(MetricsWindow)} must be greater than TimeSpan.Zero.", nameof(MetricsWindow));
        }

        if (MaxErrorRatePercent < 0.0 || MaxErrorRatePercent > 100.0)
        {
            throw new System.ArgumentException(
                $"{nameof(MaxErrorRatePercent)} must be between 0 and 100.", nameof(MaxErrorRatePercent));
        }

        if (MaxQueueDepth <= 0)
        {
            throw new System.ArgumentException(
                $"{nameof(MaxQueueDepth)} must be greater than 0.", nameof(MaxQueueDepth));
        }

        if (CriticalQueueDepth <= MaxQueueDepth)
        {
            throw new System.ArgumentException(
                $"{nameof(CriticalQueueDepth)} must be greater than {nameof(MaxQueueDepth)}.",
                nameof(CriticalQueueDepth));
        }

        if (MinThroughputPerSecond < 0)
        {
            throw new System.ArgumentException(
                $"{nameof(MinThroughputPerSecond)} must be non-negative.", nameof(MinThroughputPerSecond));
        }
    }

    #endregion Methods
}
