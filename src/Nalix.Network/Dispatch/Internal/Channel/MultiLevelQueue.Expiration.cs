using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Shared.Memory.Pools;

namespace Nalix.Network.Dispatch.Internal.Channel;

/// <summary>
/// Priority-based packet queue with support for expiration, statistics, and background cleanup.
/// </summary>
internal sealed partial class MultiLevelQueue<TPacket> where TPacket : IPacket
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
    public void Flush() => this.ClearInternal();

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
    public System.Int32 Flush(PacketPriority priority)
    {
        System.Int32 index = (System.Int32)priority;
        System.Int32 removed = this.FlushInternal(index);

        if (removed > 0)
        {
            _ = System.Threading.Interlocked.Add(ref this._totalCount, -removed);
            _ = System.Threading.Interlocked.Exchange(ref this._priorityCounts[index], 0);

            if (this._options.EnableMetrics)
            {
                this._expiredCounts![index] = 0;
                this.ClearStatistics(index);
            }
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
    public System.Threading.Tasks.Task<System.Int32> SweepExpiredAsync() => this.PruneExpiredInternalAsync();

    /// <summary>
    /// Starts a background task to periodically remove expired packets.
    /// </summary>
    /// <param name="interval">Time interval between cleanup checks.</param>
    /// <param name="cancellationToken">Identifier to stop the background task.</param>
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
                    _ = await this.SweepExpiredAsync();
                }
                catch (System.OperationCanceledException) { break; }
                catch { /* Optional: Logging */ }
            }
        }, cancellationToken);

    #endregion Public Methods

    #region Private Methods

    private System.Int32 FlushInternal(System.Int32 index)
    {
        System.Int32 cleared = 0;
        var reader = this._priorityChannels[index].Reader;

        while (reader.TryRead(out TPacket? packet))
        {
            packet.Dispose();
            cleared++;
        }

        return cleared;
    }

    /// <summary>
    /// Internal implementation for removing expired packets.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private async System.Threading.Tasks.Task<System.Int32> PruneExpiredInternalAsync()
    {
        System.Int32 totalExpired = 0;

        for (System.Int32 i = 0; i < this._priorityCount; i++)
        {
            var reader = this._priorityChannels[i].Reader;
            var writer = this._priorityChannels[i].Writer;

            System.Collections.Generic.List<TPacket> buffer = ListPool<TPacket>.Instance.Rent();

            while (reader.TryRead(out TPacket? packet))
            {
                if (packet.IsExpired(this._options.Timeout))
                {
                    packet.Dispose();
                    totalExpired++;

                    if (this._options.EnableMetrics)
                    {
                        this._expiredCounts![i]++;
                    }
                }
                else
                {
                    buffer.Add(packet);
                }
            }

            for (System.Int32 j = 0; j < buffer.Count; j++)
            {
                await writer.WriteAsync(buffer[j]);
            }

            buffer.Clear();
            ListPool<TPacket>.Instance.Return(buffer);
        }

        if (totalExpired > 0)
        {
            _ = System.Threading.Interlocked.Add(ref this._totalCount, -totalExpired);
        }

        return totalExpired;
    }

    /// <summary>
    /// Removes all packets from all queues and resets the total count.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ClearInternal()
    {
        System.Int32 totalCleared = 0;

        for (System.Int32 i = 0; i < this._priorityCount; i++)
        {
            System.Int32 removed = this.FlushInternal(i);
            totalCleared += removed;

            if (this._options.EnableMetrics)
            {
                _ = System.Threading.Interlocked.Exchange(ref this._priorityCounts[i], 0);
                this._expiredCounts![i] = 0;
                this.ClearStatistics(i);
            }
        }

        if (totalCleared > 0)
        {
            _ = System.Threading.Interlocked.Exchange(ref this._totalCount, 0);
        }
    }

    /// <summary>
    /// Drains all packets from a reader and disposes them.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 DrainAndDisposePackets(
        System.Threading.Channels.ChannelReader<TPacket> reader)
    {
        System.Int32 count = 0;
        while (reader.TryRead(out TPacket? packet))
        {
            packet.Dispose();
            count++;
        }

        return count;
    }

    #endregion Private Methods
}