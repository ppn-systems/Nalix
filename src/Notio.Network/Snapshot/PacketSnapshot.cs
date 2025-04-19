using Notio.Common.Package.Enums;
using System.Collections.Generic;

namespace Notio.Network.Snapshot;

/// <summary>
/// Provides detailed statistics and performance metrics for a priority-based packet queue.
/// </summary>
public record PacketSnapshot
{
    /// <summary>
    /// Gets the total uptime of the queue system in seconds since the tracking started.
    /// </summary>
    public int UptimeSeconds { get; init; }

    /// <summary>
    /// Gets the total number of packets currently in the queue across all priority levels.
    /// </summary>
    public int TotalPendingPackets { get; init; }

    /// <summary>
    /// Gets the average packet processing time in milliseconds.
    /// </summary>
    public float AvgProcessingTimeMs { get; init; }

    /// <summary>
    /// Gets the per-priority level statistics, grouped by <see cref="PacketPriority"/>.
    /// </summary>
    public Dictionary<PacketPriority, PriorityQueueSnapshot> PriorityLevel { get; init; } = [];
}
