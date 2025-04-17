using Notio.Common.Package.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Network.Dispatcher.Queue;

public sealed partial class PacketQueue<TPacket> where TPacket : Common.Package.IPacket
{
    #region Public Methods

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

        int priorityIndex = (int)packet.Priority;

        if (_maxQueueSize > 0 && _totalCount >= _maxQueueSize)
            return false;

        if (_priorityChannels[priorityIndex].Writer.TryWrite(packet))
        {
            Interlocked.Increment(ref _totalCount);
            Interlocked.Increment(ref _priorityCounts[priorityIndex]);

            if (_collectStatistics)
                Interlocked.Increment(ref _enqueuedCounts[priorityIndex]);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Dequeues a packet from the queue according to priority order.
    /// </summary>
    /// <returns>The dequeued packet.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TPacket Dequeue()
    {
        if (TryDequeue(out TPacket? packet))
            return packet;

        throw new InvalidOperationException("Cannot dequeue from an empty queue.");
    }

    /// <summary>
    /// Get a packet from the queue in priority order
    /// </summary>
    /// <param name="packet">The dequeued packet, if available</param>
    /// <returns>True if a packet was retrieved, False if the queue is empty</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue([NotNullWhen(true)] out TPacket? packet)
    {
        long startTicks = _collectStatistics ? Stopwatch.GetTimestamp() : 0;

        // Iterate from highest to lowest priority
        for (int i = _priorityCount - 1; i >= 0; i--)
        {
            while (_priorityChannels[i].Reader.TryRead(out var tempPacket))
            {
                Interlocked.Decrement(ref _priorityCounts[i]);
                Interlocked.Decrement(ref _totalCount);

                bool isExpired = _packetTimeout != TimeSpan.Zero && tempPacket.IsExpired(_packetTimeout);
                bool isValid = !_validateOnDequeue || tempPacket.IsValid();

                if (isExpired)
                {
                    if (_collectStatistics)
                        Interlocked.Increment(ref _expiredCounts[i]);

                    tempPacket.Dispose();
                    continue;
                }

                if (!isValid)
                {
                    if (_collectStatistics)
                        Interlocked.Increment(ref _invalidCounts[i]);

                    tempPacket.Dispose();
                    continue;
                }

                if (_collectStatistics)
                {
                    Interlocked.Increment(ref _dequeuedCounts[i]);
                    UpdatePerformanceStats(startTicks);
                }

                packet = tempPacket;
                return true;
            }
        }

        packet = default;
        return false;
    }

    /// <summary>
    /// Try to get multiple packets at once
    /// </summary>
    /// <param name="maxCount">Maximum number of packets to retrieve</param>
    /// <returns>List of valid packets</returns>
    public List<TPacket> DequeueBatch(int maxCount = 100)
    {
        List<TPacket> result = new(Math.Min(maxCount, _totalCount));

        for (int i = 0; i < maxCount; i++)
        {
            if (!TryDequeue(out var packet)) break;
            result.Add(packet);
        }

        return result;
    }

    /// <summary>
    /// Get the number of packets for each priority level
    /// </summary>
    public Dictionary<PacketPriority, int> GetQueueSizeByPriority()
    {
        Dictionary<PacketPriority, int> result = [];

        for (int i = 0; i < _priorityCount; i++)
        {
            result[(PacketPriority)i] = Volatile.Read(ref _priorityCounts[i]);
        }

        return result;
    }

    #endregion
}
