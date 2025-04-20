using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Network.Dispatch.Queue;

public sealed partial class PacketPriorityQueue<TPacket> where TPacket : Common.Package.IPacket
{
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
        long startTicks = _options.EnableMetrics ? Stopwatch.GetTimestamp() : 0;

        // Iterate from highest to lowest priority
        for (int i = _priorityCount - 1; i >= 0; i--)
        {
            while (_priorityChannels[i].Reader.TryRead(out var tempPacket))
            {
                Interlocked.Decrement(ref _priorityCounts[i]);
                Interlocked.Decrement(ref _totalCount);

                bool isExpired = _options.Timeout != TimeSpan.Zero
                    && tempPacket.IsExpired(_options.Timeout);
                bool isValid = !_options.EnableValidation || tempPacket.IsValid();

                if (isExpired)
                {
                    if (_options.EnableMetrics)
                        Interlocked.Increment(ref _expiredCounts[i]);

                    tempPacket.Dispose();
                    continue;
                }

                if (!isValid)
                {
                    if (_options.EnableMetrics)
                        Interlocked.Increment(ref _invalidCounts[i]);

                    tempPacket.Dispose();
                    continue;
                }

                if (_options.EnableMetrics)
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
}
