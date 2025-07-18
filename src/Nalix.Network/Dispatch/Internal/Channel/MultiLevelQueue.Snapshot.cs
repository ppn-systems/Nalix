using Nalix.Common.Abstractions;
using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Network.Snapshot;

namespace Nalix.Network.Dispatch.Channel;

internal sealed partial class MultiLevelQueue<TPacket> : ISnapshot<PacketSnapshot> where TPacket : IPacket
{
    #region Properties

    /// <summary>
    /// Gets a value indicating whether the queue is currently empty across all priorities.
    /// </summary>
    /// <remarks>
    /// This checks the total count of enqueued packets and returns <c>true</c> if zero.
    /// </remarks>
    public System.Boolean IsEmpty => this.TotalPendingCount == 0;

    /// <summary>
    /// Gets the total number of packets currently enqueued across all priority levels.
    /// </summary>
    /// <remarks>
    /// This value is updated atomically and reflects real-time state.
    /// </remarks>
    public System.Int32 TotalPendingCount => System.Threading.Volatile.Read(ref this._totalCount);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Retrieves the current number of packets in the queue.
    /// Optionally, you can filter the count by a specific priority level.
    /// </summary>
    /// <param name="priority">
    /// The priority level of the queue to filter by. If <c>null</c>, the count for all priorities is returned.
    /// </param>
    /// <returns>
    /// The total number of packets in the queue if no priority is specified,
    /// or the number of packets in the queue with the specified priority.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Int32 GetQueueLength(PacketPriority? priority = null) => priority.HasValue ? this._priorityCounts[(System.Int32)priority.Value] : this._totalCount;

    /// <summary>
    /// Returns a snapshot of the number of pending packets per priority level.
    /// </summary>
    /// <returns>
    /// A dictionary containing each <see cref="PacketPriority"/> and its corresponding packet count.
    /// </returns>
    /// <remarks>
    /// This method is thread-safe and reflects the queue state at the time of the call.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.Dictionary<PacketPriority, System.Int32> Snapshot()
    {
        System.Collections.Generic.Dictionary<PacketPriority, System.Int32> result = [];

        for (System.Int32 i = 0; i < this._priorityCount; i++)
        {
            result[(PacketPriority)i] = System.Threading.Volatile.Read(ref this._priorityCounts[i]);
        }

        return result;
    }

    /// <summary>
    /// Retrieves the current queue statistics, including total pending packets,
    /// packet counts by priority level, average processing time, and uptime.
    /// </summary>
    /// <returns>
    /// A <see cref="PacketSnapshot"/> object containing the current queue statistics:
    /// - TotalPendingPackets: Total number of packets currently in the queue.
    /// - PerPriorityStats: A dictionary containing the count of packets for each priority level.
    /// - AvgProcessingTimeMs: The average time (in milliseconds) taken to process a packet.
    /// - UptimeSeconds: The total uptime of the queue in seconds.
    /// </returns>
    /// <remarks>
    /// This method provides an overview of the current state of the queue, including how many
    /// packets are pending and the overall processing performance. If metrics collection is disabled
    /// or if the queue timer is unavailable, an empty snapshot will be returned.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketSnapshot GetSnapshot()
    {
        if (!this._options.EnableMetrics)
        {
            return new PacketSnapshot();
        }

        System.Collections.Generic.Dictionary<PacketPriority, PriorityQueueSnapshot> stats = [];

        this.CollectStatisticsInternal(stats);

        System.Single avgProcessingMs = 0;

        return new PacketSnapshot
        {
            TotalPendingPackets = this.Count,
            PerPriorityStats = stats,
            AvgProcessingTimeMs = avgProcessingMs
        };
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Clears all collected statistics for a specific priority level.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ClearStatistics(System.Int32 index)
    {
        if (this._options.EnableMetrics)
        {
            this._expiredCounts![index] = 0;
            this._rejectedCounts![index] = 0;
            this._enqueuedCounts![index] = 0;
            this._dequeuedCounts![index] = 0;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CollectStatisticsInternal(
        System.Collections.Generic.Dictionary<PacketPriority, PriorityQueueSnapshot> stats)
    {
        for (System.Int32 i = 0; i < this._priorityCount; i++)
        {
            stats[(PacketPriority)i] = this._options.EnableMetrics
                ? new PriorityQueueSnapshot
                {
                    PendingPackets = System.Threading.Volatile.Read(ref this._priorityCounts[i]),
                    TotalEnqueued = this._enqueuedCounts![i],
                    TotalDequeued = this._dequeuedCounts![i],
                    TotalExpiredPackets = this._expiredCounts![i],
                    TotalRejectedPackets = this._rejectedCounts![i]
                }
                : new PriorityQueueSnapshot
                {
                    PendingPackets = System.Threading.Volatile.Read(ref this._priorityCounts[i]),
                    TotalEnqueued = 0,
                    TotalDequeued = 0,
                    TotalExpiredPackets = 0,
                    TotalRejectedPackets = 0
                };
        }
    }

    #endregion Private Methods
}