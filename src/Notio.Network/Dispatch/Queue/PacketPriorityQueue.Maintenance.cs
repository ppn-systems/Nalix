using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Dispatch.Queue;

public sealed partial class PacketPriorityQueue<TPacket> where TPacket : Common.Package.IPacket
{
    #region Public Methods

    /// <summary>
    /// Remove expired packets from all queues
    /// </summary>
    /// <returns>Number of packets removed</returns>
    public async Task<int> RemoveExpiredPacketsAsync()
        => await this.RemoveExpiredPacketsInternalAsync();

    /// <summary>
    /// Clear all packets from the queue
    /// </summary>
    public void Clear() => this.ClearInternal();

    /// <summary>
    /// Initialize a periodic task to remove expired packets
    /// </summary>
    /// <param name="interval">Time interval between checks</param>
    /// <param name="cancellationToken">Token to cancel the task</param>
    /// <returns>Background running task</returns>
    public Task StartExpirationCheckerAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken);
                    await RemoveExpiredPacketsAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Log errors here if needed
                }
            }
        }, cancellationToken);
    }

    #endregion

    #region Private Methods

    private async Task<int> RemoveExpiredPacketsInternalAsync()
    {
        int removedCount = 0;

        for (int i = 0; i < _priorityCount; i++)
        {
            Queue<TPacket> tempQueue = new();

            while (_priorityChannels[i].Reader.TryRead(out var packet))
            {
                if (packet.IsExpired(_options.PacketTimeout))
                {
                    packet.Dispose();
                    removedCount++;

                    if (_options.CollectStatistics) _expiredCounts[i]++;
                }
                else
                {
                    tempQueue.Enqueue(packet);
                }
            }

            // Re-insert non-expired packets
            while (tempQueue.Count > 0)
            {
                await _priorityChannels[i].Writer.WriteAsync(tempQueue.Dequeue());
            }
        }

        Interlocked.Add(ref _totalCount, -removedCount);
        return removedCount;
    }

    private void ClearInternal()
    {
        for (int i = 0; i < _priorityCount; i++)
        {
            while (_priorityChannels[i].Reader.TryRead(out var packet))
                packet.Dispose();
        }

        Interlocked.Exchange(ref _totalCount, 0);
    }

    #endregion
}
