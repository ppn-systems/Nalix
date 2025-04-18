using Notio.Common.Package.Enums;
using System.Collections.Generic;

namespace Notio.Network.Dispatch.Queue.Statistics;

/// <summary>
/// Provides detailed statistics and performance metrics for a priority-based packet queue.
/// </summary>
public class PacketStatistics
{
    /// <summary>
    /// Gets the total number of packets currently in the queue across all priority levels.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the total uptime of the queue system in seconds since the tracking started.
    /// </summary>
    public int UptimeSeconds { get; init; }

    /// <summary>
    /// Gets the average packet processing time in milliseconds.
    /// </summary>
    public float ArgProcessingTimeMs { get; init; }

    /// <summary>
    /// Gets the per-priority level statistics, grouped by <see cref="PacketPriority"/>.
    /// </summary>
    public Dictionary<PacketPriority, PriorityStatistics> PerPriorityStats { get; init; } = [];
}
