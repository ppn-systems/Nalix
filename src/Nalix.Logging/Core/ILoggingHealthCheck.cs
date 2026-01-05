// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Core;

/// <summary>
/// Represents the health status of the logging system.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Logging system is operating normally.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Logging system is operational but experiencing issues.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Logging system is not operational.
    /// </summary>
    Unhealthy = 2
}

/// <summary>
/// Defines a contract for logging health check monitoring.
/// </summary>
public interface ILoggingHealthCheck
{
    /// <summary>
    /// Gets the current health status of the logging system.
    /// </summary>
    HealthStatus Status { get; }

    /// <summary>
    /// Gets the throughput in entries per second over the metrics window.
    /// </summary>
    System.Double ThroughputPerSecond { get; }

    /// <summary>
    /// Gets the error rate as a percentage (0-100) over the metrics window.
    /// </summary>
    System.Double ErrorRatePercent { get; }

    /// <summary>
    /// Gets the current queue depth across all targets.
    /// </summary>
    System.Int32 CurrentQueueDepth { get; }

    /// <summary>
    /// Gets the total number of log entries processed since startup.
    /// </summary>
    System.Int64 TotalEntriesProcessed { get; }

    /// <summary>
    /// Gets the total number of errors encountered since startup.
    /// </summary>
    System.Int64 TotalErrors { get; }

    /// <summary>
    /// Gets diagnostic information about the logging system health.
    /// </summary>
    /// <returns>A string containing health diagnostics.</returns>
    System.String GetDiagnostics();

    /// <summary>
    /// Performs a health check and updates metrics.
    /// </summary>
    /// <returns>The current health status.</returns>
    HealthStatus CheckHealth();
}
