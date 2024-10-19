using Nalix.Common.Package.Enums;

namespace Nalix.Network.Observability;

/// <summary>
/// Provides detailed statistics and performance metrics for a priority-based packet queue.
/// </summary>
public record PacketStats
{
    /// <summary>
    /// Gets the total number of packets currently in the queue across all priority levels.
    /// </summary>
    public System.Int32 TotalPendingPackets { get; init; }

    /// <summary>
    /// Gets the average packet processing time in milliseconds.
    /// </summary>
    public System.Single AvgProcessingTimeMs { get; init; }

    /// <summary>
    /// Gets the per-priority level statistics, grouped by <see cref="PacketPriority"/>.
    /// </summary>
    public System.Collections.Generic.Dictionary<PacketPriority, PriorityQueueStats> PerPriorityStats { get; init; } = [];
}