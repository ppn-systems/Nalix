using Notio.Common.Package.Enums;
using Notio.Network.Snapshot;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Network.Dispatch.Queue;

public sealed partial class PacketPriorityQueue<TPacket> where TPacket : Common.Package.IPacket
{
    #region Public Methods

    /// <summary>
    /// Get queue statistics
    /// </summary>
    public PacketSnapshot GetStatistics()
    {
        if (!_options.CollectStatistics || _queueTimer == null)
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
    private void ClearStatistics(int index)
    {
        _expiredCounts[index] = 0;
        _invalidCounts[index] = 0;
        _enqueuedCounts[index] = 0;
        _dequeuedCounts[index] = 0;
    }

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
                TotalRejectedPackets = _invalidCounts[i]
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
