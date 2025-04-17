using Notio.Common.Package.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Notio.Network.Dispatcher.Queue;

public sealed partial class PacketQueue<TPacket> where TPacket : Common.Package.IPacket
{
    /// <summary>
    /// Add a packet to the queue
    /// </summary>
    /// <param name="packet">Packet to add</param>
    /// <returns>True if added successfully, False if the queue is full</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Enqueue(TPacket packet)
    {
        if (packet == null)
            return false;

        if (_isThreadSafe)
        {
            lock (_syncLock)
            {
                return this.EnqueueInternal(packet);
            }
        }
        else
        {
            return this.EnqueueInternal(packet);
        }
    }

    /// <summary>
    /// Get a packet from the queue in priority order
    /// </summary>
    /// <param name="packet">The dequeued packet, if available</param>
    /// <returns>True if a packet was retrieved, False if the queue is empty</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out TPacket? packet)
    {
        if (_isThreadSafe)
        {
            lock (_syncLock)
            {
                return this.TryDequeueInternal(out packet);
            }
        }
        else
        {
            return this.TryDequeueInternal(out packet);
        }
    }

    /// <summary>
    /// Try to get multiple packets at once
    /// </summary>
    /// <param name="maxCount">Maximum number of packets to retrieve</param>
    /// <returns>List of valid packets</returns>
    public List<TPacket> DequeueBatch(int maxCount)
    {
        List<TPacket> result = new(Math.Min(maxCount, Count));

        if (_isThreadSafe)
        {
            lock (_syncLock)
                this.DequeueBatchInternal(result, maxCount);
        }
        else
        {
            this.DequeueBatchInternal(result, maxCount);
        }

        return result;
    }

    /// <summary>
    /// Get number of packets for each priority level
    /// </summary>
    public Dictionary<PacketPriority, int> GetQueueSizeByPriority()
    {
        Dictionary<PacketPriority, int> result = [];

        if (_isThreadSafe)
        {
            lock (_syncLock)
            {
                for (int i = 0; i < _priorityCount; i++)
                    result[(PacketPriority)i] = _priorityQueues[i].Count;
            }
        }
        else
        {
            for (int i = 0; i < _priorityCount; i++)
                result[(PacketPriority)i] = _priorityQueues[i].Count;
        }

        return result;
    }

    #region Privates Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EnqueueInternal(TPacket packet)
    {
        // Check queue size limit if any
        if (_maxQueueSize > 0 && _totalCount >= _maxQueueSize)
        {
            return false;
        }

        int priorityIndex = (int)packet.Priority;
        _priorityQueues[priorityIndex].Enqueue(packet);
        _totalCount++;

        if (_collectStatistics)
        {
            _enqueuedCounts[priorityIndex]++;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryDequeueInternal(out TPacket? packet)
    {
        long startTicks = _collectStatistics ? Stopwatch.GetTimestamp() : 0;

        // Iterate from highest to lowest priority
        for (int i = _priorityCount - 1; i >= 0; i--)
        {
            Queue<TPacket> queue = _priorityQueues[i];

            while (queue.Count > 0)
            {
                packet = queue.Dequeue();
                _totalCount--;

                bool isValid = true;
                bool isExpired = false;

                // Check expiration if needed
                if (_packetTimeout != TimeSpan.Zero)
                {
                    isExpired = packet.IsExpired(_packetTimeout);
                    if (isExpired && _collectStatistics)
                    {
                        _expiredCounts[i]++;
                    }
                }

                // Check validity if needed
                if (_validateOnDequeue && !isExpired)
                {
                    isValid = packet.IsValid();
                    if (!isValid && _collectStatistics)
                    {
                        _invalidCounts[i]++;
                    }
                }

                // If the packet is valid and not expired, return it
                if (!isExpired && isValid)
                {
                    if (_collectStatistics)
                    {
                        _dequeuedCounts[i]++;
                        UpdatePerformanceStats(startTicks);
                    }
                    return true;
                }

                // Packet is invalid or expired, free its resources
                packet.Dispose();

                // Continue checking next packet in same queue
                if (queue.Count > 0)
                    continue;
            }
        }

        packet = default; // Ensure packet is explicitly set to default (null for reference types)
        return false;
    }

    private void DequeueBatchInternal(List<TPacket> result, int maxCount)
    {
        int dequeued = 0;

        while (dequeued < maxCount)
        {
            if (TryDequeueInternal(out TPacket? packet) && packet != null)
            {
                result.Add(packet);
                dequeued++;
            }
            else
            {
                break;
            }
        }
    }

    #endregion
}
