using Notio.Common.Package.Enums;
using Notio.Network.Dispatcher.Queue.Statistics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Notio.Network.Dispatcher.Queue;

public sealed partial class PacketQueue<TPacket> where TPacket : Common.Package.IPacket
{
    /// <summary>
    /// Get queue statistics
    /// </summary>
    public PacketQueueStatistics GetStatistics()
    {
        if (!_collectStatistics || _queueTimer == null)
            return new PacketQueueStatistics();

        Dictionary<PacketPriority, PriorityStatistics> stats = [];

        if (_isThreadSafe)
        {
            lock (_syncLock)
            {
                this.CollectStatisticsInternal(stats);
            }
        }
        else
        {
            this.CollectStatisticsInternal(stats);
        }

        float avgProcessingMs = 0;
        if (_packetsProcessed > 0)
            avgProcessingMs = (float)(_totalProcessingTicks * 1000.0 / Stopwatch.Frequency) / _packetsProcessed;

        return new PacketQueueStatistics
        {
            TotalCount = Count,
            PerPriorityStats = stats,
            AvgProcessingTimeMs = avgProcessingMs,
            UptimeSeconds = (int)_queueTimer.Elapsed.TotalSeconds // _queueTimer is guaranteed to be non-null here
        };
    }

    #region Private Methods

    private void CollectStatisticsInternal(Dictionary<PacketPriority, PriorityStatistics> stats)
    {
        for (int i = 0; i < _priorityCount; i++)
        {
            var priority = (PacketPriority)i;
            stats[priority] = new PriorityStatistics
            {
                CurrentQueueSize = _priorityQueues[i].Count,
                EnqueuedCount = _enqueuedCounts[i],
                DequeuedCount = _dequeuedCounts[i],
                ExpiredCount = _expiredCounts[i],
                InvalidCount = _invalidCounts[i]
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
