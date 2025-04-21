using Notio.Common.Package.Enums;
using Notio.Network.Snapshot;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Network.Dispatch.Queue;

public sealed partial class DispatchQueue<TPacket> where TPacket : Common.Package.IPacket
{
    #region Properties

    /// <summary>
    /// Gets a value indicating whether the queue is currently empty across all priorities.
    /// </summary>
    /// <remarks>
    /// This checks the total count of enqueued packets and returns <c>true</c> if zero.
    /// </remarks>
    public bool IsEmpty => TotalPendingCount == 0;

    /// <summary>
    /// Gets the total number of packets currently enqueued across all priority levels.
    /// </summary>
    /// <remarks>
    /// This value is updated atomically and reflects real-time state.
    /// </remarks>
    public int TotalPendingCount => Volatile.Read(ref _totalCount);

    #endregion

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetQueueLength(PacketPriority? priority = null)
    {
        if (priority.HasValue)
            return _priorityCounts[(int)priority.Value];

        return _totalCount;
    }

    /// <summary>
    /// Returns a snapshot of the number of pending packets per priority level.
    /// </summary>
    /// <returns>
    /// A dictionary containing each <see cref="PacketPriority"/> and its corresponding packet count.
    /// </returns>
    /// <remarks>
    /// This method is thread-safe and reflects the queue state at the time of the call.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<PacketPriority, int> Snapshot()
    {
        Dictionary<PacketPriority, int> result = [];

        for (int i = 0; i < _priorityCount; i++)
        {
            result[(PacketPriority)i] = Volatile.Read(ref _priorityCounts[i]);
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
    /// - PriorityLevel: A dictionary containing the count of packets for each priority level.
    /// - AvgProcessingTimeMs: The average time (in milliseconds) taken to process a packet.
    /// - UptimeSeconds: The total uptime of the queue in seconds.
    /// </returns>
    /// <remarks>
    /// This method provides an overview of the current state of the queue, including how many
    /// packets are pending and the overall processing performance. If metrics collection is disabled
    /// or if the queue timer is unavailable, an empty snapshot will be returned.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketSnapshot GetStatistics()
    {
        if (!_options.EnableMetrics || _queueTimer == null)
            return new PacketSnapshot();

        Dictionary<PacketPriority, PriorityQueueSnapshot> stats = [];

        this.CollectStatisticsInternal(stats);

        float avgProcessingMs = 0;
        if (_packetsProcessed > 0)
            avgProcessingMs = (float)(_totalProcessingTicks * 1000.0 / Stopwatch.Frequency) / _packetsProcessed;

        return new PacketSnapshot
        {
            TotalPendingPackets = Count,
            PriorityLevel = stats,
            AvgProcessingTimeMs = avgProcessingMs,
            UptimeSeconds = (int)_queueTimer.Elapsed.TotalSeconds // _queueTimer is guaranteed to be non-null here
        };
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Clears all collected statistics for a specific priority level.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearStatistics(int index)
    {
        _expiredCounts[index] = 0;
        _rejectedCounts[index] = 0;
        _enqueuedCounts[index] = 0;
        _dequeuedCounts[index] = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CollectStatisticsInternal(Dictionary<PacketPriority, PriorityQueueSnapshot> stats)
    {
        for (int i = 0; i < _priorityCount; i++)
        {
            stats[(PacketPriority)i] = new PriorityQueueSnapshot
            {
                PendingPackets = Volatile.Read(ref _priorityCounts[i]),
                TotalEnqueued = _enqueuedCounts[i],
                TotalDequeued = _dequeuedCounts[i],
                TotalExpiredPackets = _expiredCounts[i],
                TotalRejectedPackets = _rejectedCounts[i]
            };
        }
    }

    /// <summary>
    /// Update performance statistics
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdatePerformanceStats(long startTicks)
    {
        long endTicks = Stopwatch.GetTimestamp();
        long elapsed = endTicks - startTicks;

        _totalProcessingTicks += elapsed;
        _packetsProcessed++;
    }

    #endregion
}
