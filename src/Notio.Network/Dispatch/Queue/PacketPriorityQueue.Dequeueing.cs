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
    /// Dequeues multiple packets from the queue until a specified condition is met.
    /// </summary>
    /// <param name="maxCount">The maximum number of packets to retrieve.</param>
    /// <param name="shouldStop">A predicate to stop dequeuing when it returns true.</param>
    /// <returns>A list of dequeued packets.</returns>
    public List<TPacket> DequeueWhile(int maxCount, Func<TPacket, bool> shouldStop)
    {
        List<TPacket> result = [];
        int dequeuedCount = 0;

        while (dequeuedCount < maxCount && TryDequeue(out var packet))
        {
            if (shouldStop(packet)) break;
            result.Add(packet);
            dequeuedCount++;
        }

        return result;
    }

    /// <summary>
    /// Try to dequeue a valid packet, and returns the expired/invalid packet if any.
    /// </summary>
    /// <param name="packet">The dequeued packet if valid, null if none are dequeued.</param>
    /// <param name="invalidPacket">A packet that was invalid or expired, null if no such packet exists.</param>
    /// <returns>True if a valid packet was dequeued, false if no valid packet was found.</returns>
    public bool TryDequeueWithInvalid(
        [NotNullWhen(true)] out TPacket? packet,
        [NotNullWhen(false)] out TPacket? invalidPacket)
    {
        long startTicks = _options.EnableMetrics ? Stopwatch.GetTimestamp() : 0;

        packet = default;
        invalidPacket = default; // Initialize invalidPacket to default (null)

        for (int i = _priorityCount - 1; i >= 0; i--)
        {
            while (_priorityChannels[i].Reader.TryRead(out TPacket? tempPacket))
            {
                Interlocked.Decrement(ref _priorityCounts[i]);
                Interlocked.Decrement(ref _totalCount);

                bool isExpired = _options.Timeout != TimeSpan.Zero && tempPacket.IsExpired(_options.Timeout);
                bool isValid = !_options.EnableValidation || tempPacket.IsValid();

                if (isExpired || !isValid)
                {
                    invalidPacket = tempPacket; // Return the invalid or expired packet for inspection
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

        return false;
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

                //bool isExpired = _options.Timeout != TimeSpan.Zero
                //    && tempPacket.IsExpired(_options.Timeout);
                //bool isValid = !_options.EnableValidation || tempPacket.IsValid();

                //if (isExpired)
                //{
                //    if (_options.EnableMetrics)
                //        Interlocked.Increment(ref _expiredCounts[i]);

                //    tempPacket.Dispose();
                //    continue;
                //}

                //if (!isValid)
                //{
                //    if (_options.EnableMetrics)
                //        Interlocked.Increment(ref _rejectedCounts[i]);

                //    tempPacket.Dispose();
                //    continue;
                //}

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
