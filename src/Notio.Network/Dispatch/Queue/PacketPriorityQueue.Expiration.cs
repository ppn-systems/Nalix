using Notio.Common.Package.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Notio.Network.Dispatch.Queue;

/// <summary>
/// Priority-based packet queue with support for expiration, statistics, and background cleanup.
/// </summary>
public sealed partial class PacketPriorityQueue<TPacket> where TPacket : Common.Package.IPacket
{
    #region Public Methods

    /// <summary>
    /// Removes all packets from all priority queues.
    /// </summary>
    /// <remarks>
    /// This method will clear all packets across all priority queues. It also resets the total packet count to zero.
    /// </remarks>
    public void FlushAll() => ClearInternal();

    /// <summary>
    /// Removes all packets from the specified priority queue.
    /// </summary>
    /// <param name="priority">The priority level of the queue to purge.</param>
    /// <returns>The number of packets removed from the specified priority queue.</returns>
    /// <remarks>
    /// This method will drain and dispose all packets from the queue associated with the provided priority level.
    /// It also updates the total count of packets and resets the priority count for that level.
    /// If metrics are enabled, it will clear the associated statistics for the priority level.
    /// </remarks>
    public int FlushPriority(PacketPriority priority)
    {
        int index = (int)priority;
        int removed = PacketPriorityQueue<TPacket>.DrainAndDisposePackets(_priorityChannels[index].Reader);

        if (removed > 0)
        {
            Interlocked.Add(ref _totalCount, -removed);
            Interlocked.Exchange(ref _priorityCounts[index], 0);
            if (_options.EnableMetrics) this.ClearStatistics(index);
        }

        return removed;
    }

    /// <summary>
    /// Asynchronously removes expired packets from all priority queues.
    /// </summary>
    /// <returns>The total number of expired packets that were removed from the queues.</returns>
    /// <remarks>
    /// This method checks each packet in the queue and removes those that have expired based on the configured timeout.
    /// It will asynchronously process the removal of expired packets from all queues.
    /// </remarks>
    public Task<int> SweepExpiredAsync() => PruneExpiredInternalAsync();

    /// <summary>
    /// Starts a background task to periodically remove expired packets.
    /// </summary>
    /// <param name="interval">Time interval between cleanup checks.</param>
    /// <param name="cancellationToken">Token to stop the background task.</param>
    /// <returns>The background task instance that is running the cleanup operation.</returns>
    /// <remarks>
    /// This method runs a background task that periodically checks for expired packets and removes them.
    /// The task will run until the cancellation token is triggered.
    /// </remarks>
    public Task RunExpirationCleanerAsync(TimeSpan interval, CancellationToken cancellationToken = default)
        => Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken);
                    await SweepExpiredAsync();
                }
                catch (OperationCanceledException) { break; }
                catch { /* Optional: Logging */ }
            }
        }, cancellationToken);

    #endregion

    #region Private Methods

    /// <summary>
    /// Internal implementation for removing expired packets.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<int> PruneExpiredInternalAsync()
    {
        int totalExpired = 0;

        for (int i = 0; i < _priorityCount; i++)
        {
            var reader = _priorityChannels[i].Reader;
            var writer = _priorityChannels[i].Writer;
            Queue<TPacket> temp = new();

            while (reader.TryRead(out TPacket? packet))
            {
                if (packet.IsExpired(_options.Timeout))
                {
                    packet.Dispose();
                    totalExpired++;
                    if (_options.EnableMetrics) _expiredCounts[i]++;
                }
                else
                {
                    temp.Enqueue(packet);
                }
            }

            while (temp.TryDequeue(out TPacket? p)) await writer.WriteAsync(p);
        }

        if (totalExpired > 0)
            Interlocked.Add(ref _totalCount, -totalExpired);

        return totalExpired;
    }

    /// <summary>
    /// Removes all packets from all queues and resets the total count.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearInternal()
    {
        int totalCleared = 0;

        for (int i = 0; i < _priorityCount; i++)
            totalCleared += PacketPriorityQueue<TPacket>.DrainAndDisposePackets(_priorityChannels[i].Reader);

        if (totalCleared > 0)
            Interlocked.Exchange(ref _totalCount, 0);
    }

    /// <summary>
    /// Drains all packets from a reader and disposes them.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DrainAndDisposePackets(ChannelReader<TPacket> reader)
    {
        int count = 0;
        while (reader.TryRead(out TPacket? packet))
        {
            packet.Dispose();
            count++;
        }

        return count;
    }

    #endregion
}
