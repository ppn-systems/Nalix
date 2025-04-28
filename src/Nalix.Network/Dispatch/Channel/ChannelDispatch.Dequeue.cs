using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Channel;

public sealed partial class ChannelDispatch<TPacket> where TPacket : Common.Package.IPacket
{
    /// <summary>
    /// Retrieves and removes the next available packet from the queue, following priority order.
    /// </summary>
    /// <returns>The next valid packet in the queue.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TPacket Dequeue()
    {
        if (this.TryDequeue(out TPacket? packet)) return packet;
        throw new InvalidOperationException("Cannot dequeue from an empty queue.");
    }

    /// <summary>
    /// Retrieves and removes packets from the queue until the maximum count is reached
    /// or the specified stopping condition returns <c>true</c>.
    /// </summary>
    /// <param name="predicate">
    /// A predicate that determines whether dequeuing should stop. If it returns <c>true</c>,
    /// the method stops dequeuing.
    /// </param>
    /// <returns>A list of packets dequeued before the stop condition was met.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<TPacket> Dequeue(Func<TPacket, bool> predicate)
    {
        List<TPacket> result = [];

        while (this.TryDequeue(out TPacket? packet))
        {
            if (predicate(packet))
            {
                result.Add(packet);
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Retrieves and removes packets from the queue until the maximum count is reached
    /// or the specified stopping condition returns <c>true</c>.
    /// </summary>
    /// <param name="limit">The maximum number of packets to dequeue.</param>
    /// <param name="predicate">
    /// A predicate that determines whether dequeuing should stop. If it returns <c>true</c>,
    /// the method stops dequeuing.
    /// </param>
    /// <returns>A list of packets dequeued before the stop condition was met.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<TPacket> Dequeue(int limit, Func<TPacket, bool> predicate)
    {
        List<TPacket> result = [];
        int count = 0;

        while (count < limit && this.TryDequeue(out TPacket? packet))
        {
            if (predicate(packet))
                break;

            result.Add(packet);
            count++;
        }

        return result;
    }

    /// <summary>
    /// Dequeues up to <paramref name="limit"/> packets in priority order.
    /// </summary>
    /// <param name="limit">The maximum number of packets to dequeue. Environment to 100.</param>
    /// <returns>A list of dequeued packets. May contain fewer than <paramref name="limit"/> packets if the queue runs out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<TPacket> DequeueBatch(int limit = 100)
    {
        List<TPacket> result = new(Math.Min(limit, _totalCount));

        for (int i = 0; i < limit; i++)
        {
            if (!this.TryDequeue(out TPacket? packet))
                break;

            result.Add(packet);
        }

        return result;
    }

    /// <summary>
    /// Attempts to retrieve and remove a packet from the queue in priority order.
    /// </summary>
    /// <param name="packet">The dequeued packet, if available; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a packet was successfully dequeued; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue([NotNullWhen(true)] out TPacket? packet)
    {
        long ticks = _options.EnableMetrics ? Stopwatch.GetTimestamp() : 0;

        // Iterate from highest to lowest priority
        for (int i = _priorityCount - 1; i >= 0; i--)
        {
            while (_priorityChannels[i].Reader.TryRead(out TPacket? temp))
            {
                System.Threading.Interlocked.Decrement(ref _priorityCounts[i]);
                System.Threading.Interlocked.Decrement(ref _totalCount);

                if (_options.EnableMetrics)
                {
                    System.Threading.Interlocked.Increment(ref _dequeuedCounts[i]);
                    this.UpdatePerformanceStats(ticks);
                }

                packet = temp;
                return true;
            }
        }

        packet = default;
        return false;
    }

    /// <summary>
    /// Attempts to dequeue a valid packet from the queue. If the dequeued packet is expired or invalid,
    /// it is returned via <paramref name="rejected"/> instead.
    /// </summary>
    /// <param name="packet">The valid dequeued packet, if available; otherwise, <c>null</c>.</param>
    /// <param name="rejected">The expired or invalid packet, if any; otherwise, <c>null</c>.</param>
    /// <returns>
    /// <c>true</c> if a valid packet was dequeued; otherwise, <c>false</c> if the packet was invalid or expired.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(
        [NotNullWhen(true)] out TPacket? packet,
        [NotNullWhen(false)] out TPacket? rejected)
    {
        long startTicks = _options.EnableMetrics ? Stopwatch.GetTimestamp() : 0;

        packet = default;
        rejected = default; // Initialize rejected to default (null)

        for (int i = _priorityCount - 1; i >= 0; i--)
        {
            while (_priorityChannels[i].Reader.TryRead(out TPacket? temp))
            {
                System.Threading.Interlocked.Decrement(ref _priorityCounts[i]);
                System.Threading.Interlocked.Decrement(ref _totalCount);

                bool isValid = !_options.EnableValidation || temp.IsValid();
                bool isExpired = _options.Timeout != TimeSpan.Zero && temp.IsExpired(_options.Timeout);

                if (!isValid)
                {
                    if (_options.EnableMetrics)
                        System.Threading.Interlocked.Increment(ref _rejectedCounts[i]);

                    rejected = temp; // Assign the invalid packet
                    temp.Dispose();
                    return false; // Exit with the invalid packet
                }

                if (isExpired)
                {
                    if (_options.EnableMetrics)
                        System.Threading.Interlocked.Increment(ref _expiredCounts[i]);

                    rejected = temp; // Assign the expired packet
                    temp.Dispose();
                    return false; // Exit with the expired packet
                }

                if (_options.EnableMetrics)
                {
                    System.Threading.Interlocked.Increment(ref _dequeuedCounts[i]);
                    this.UpdatePerformanceStats(startTicks);
                }

                packet = temp;
                return true;
            }
        }

        rejected = default!;
        return false;
    }

    /// <summary>
    /// Attempts to peek at the next available packet in the queue without removing it.
    /// Scans from highest to lowest priority.
    /// </summary>
    /// <param name="packet">The next packet if available; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a packet was found; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeek([NotNullWhen(true)] out TPacket? packet)
    {
        // Iterate from highest to lowest priority
        for (int i = _priorityCount - 1; i >= 0; i--)
        {
            var reader = _priorityChannels[i].Reader;

            if (reader.TryPeek(out TPacket? temp))
            {
                packet = temp;
                return true;
            }
        }

        packet = default;
        return false;
    }
}
