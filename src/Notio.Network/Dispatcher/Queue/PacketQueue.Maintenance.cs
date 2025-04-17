using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Dispatcher.Queue;

public sealed partial class PacketQueue<TPacket> where TPacket : Common.Package.IPacket
{
    #region Public Methods

    /// <summary>
    /// Remove expired packets from all queues
    /// </summary>
    /// <returns>Number of packets removed</returns>
    public int RemoveExpiredPackets()
    {
        if (_isThreadSafe)
        {
            lock (_syncLock)
            {
                return this.RemoveExpiredPacketsInternal();
            }
        }
        else
        {
            return this.RemoveExpiredPacketsInternal();
        }
    }

    private int RemoveExpiredPacketsInternal()
    {
        int removedCount = 0;

        for (int i = 0; i < _priorityCount; i++)
        {
            Queue<TPacket> currentQueue = _priorityQueues[i];
            int queueCount = currentQueue.Count;

            if (queueCount == 0)
                continue;

            // Create a new queue to store non-expired packets
            var newQueue = new Queue<TPacket>(queueCount);

            // Check each packet
            while (currentQueue.Count > 0)
            {
                TPacket packet = currentQueue.Dequeue();

                if (packet.IsExpired(_packetTimeout))
                {
                    // Free expired packet
                    packet.Dispose();
                    removedCount++;

                    if (_collectStatistics)
                    {
                        _expiredCounts[i]++;
                    }
                }
                else
                {
                    // Keep non-expired packet
                    newQueue.Enqueue(packet);
                }
            }

            // Replace old queue with new one
            _priorityQueues[i] = newQueue;
        }

        // Update total packet count
        _totalCount -= removedCount;
        return removedCount;
    }

    /// <summary>
    /// Clear all packets from the queue
    /// </summary>
    public void Clear()
    {
        if (_isThreadSafe)
        {
            lock (_syncLock)
            {
                this.ClearInternal();
            }
        }
        else
        {
            this.ClearInternal();
        }
    }

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
                    RemoveExpiredPackets();
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

    private void ClearInternal()
    {
        for (int i = 0; i < _priorityCount; i++)
        {
            // Release resources of packets before clearing
            while (_priorityQueues[i].Count > 0)
            {
                TPacket packet = _priorityQueues[i].Dequeue();
                packet.Dispose();
            }
        }

        _totalCount = 0;
    }

    #endregion
}
