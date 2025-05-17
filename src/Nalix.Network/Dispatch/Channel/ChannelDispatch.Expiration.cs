using Nalix.Common.Package;
using Nalix.Common.Package.Enums;

namespace Nalix.Network.Dispatch.Channel;

/// <summary>
/// Priority-based packet queue with support for expiration, statistics, and background cleanup.
/// </summary>
public sealed partial class ChannelDispatch<TPacket> where TPacket : IPacket
{
    #region Public Methods

    /// <summary>
    /// Removes all packets from all priority queues.
    /// </summary>
    /// <remarks>
    /// This method will clear all packets across all priority queues. It also resets the total packet count to zero.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Flush() => ClearInternal();

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int Flush(PacketPriority priority)
    {
        int index = (int)priority;
        int removed = ChannelDispatch<TPacket>.DrainAndDisposePackets(_priorityChannels[index].Reader);

        if (removed > 0)
        {
            System.Threading.Interlocked.Add(ref _totalCount, -removed);
            System.Threading.Interlocked.Exchange(ref _priorityCounts[index], 0);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.Task<int> SweepExpiredAsync() => PruneExpiredInternalAsync();

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.Task RunExpirationCleanerAsync(
        System.TimeSpan interval,
        System.Threading.CancellationToken cancellationToken = default)
        => System.Threading.Tasks.Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(interval, cancellationToken);
                    await SweepExpiredAsync();
                }
                catch (System.OperationCanceledException) { break; }
                catch { /* Optional: Logging */ }
            }
        }, cancellationToken);

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Internal implementation for removing expired packets.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private async System.Threading.Tasks.Task<int> PruneExpiredInternalAsync()
    {
        int totalExpired = 0;

        for (int i = 0; i < _priorityCount; i++)
        {
            System.Collections.Generic.Queue<TPacket> temp = new();
            System.Threading.Channels.ChannelReader<TPacket> reader = _priorityChannels[i].Reader;
            System.Threading.Channels.ChannelWriter<TPacket> writer = _priorityChannels[i].Writer;

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
            System.Threading.Interlocked.Add(ref _totalCount, -totalExpired);

        return totalExpired;
    }

    /// <summary>
    /// Removes all packets from all queues and resets the total count.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ClearInternal()
    {
        int totalCleared = 0;

        for (int i = 0; i < _priorityCount; i++)
            totalCleared += ChannelDispatch<TPacket>.DrainAndDisposePackets(_priorityChannels[i].Reader);

        if (totalCleared > 0)
            System.Threading.Interlocked.Exchange(ref _totalCount, 0);
    }

    /// <summary>
    /// Drains all packets from a reader and disposes them.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int DrainAndDisposePackets(
        System.Threading.Channels.ChannelReader<TPacket> reader)
    {
        int count = 0;
        while (reader.TryRead(out TPacket? packet))
        {
            packet.Dispose();
            count++;
        }

        return count;
    }

    #endregion Private Methods
}
