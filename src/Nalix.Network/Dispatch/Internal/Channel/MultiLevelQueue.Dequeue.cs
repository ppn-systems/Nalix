using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Shared.Memory.Pools;

namespace Nalix.Network.Dispatch.Internal.Channel;

internal sealed partial class MultiLevelQueue<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Retrieves and removes the next available packet from the queue, following priority order.
    /// </summary>
    /// <returns>The next valid packet in the queue.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown if the queue is empty.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public TPacket Dequeue()
    {
        return this.TryDequeue(out TPacket? packet)
            ? packet
            : throw new System.InvalidOperationException("Cannot dequeue from an empty queue.");
    }

    /// <summary>
    /// Attempts to dequeue a packet from the specified priority channel.
    /// </summary>
    /// <param name="priority">
    /// The priority level of the packet to dequeue. Must be within the valid range of priorities.
    /// </param>
    /// <returns>
    /// The dequeued packet if available.
    /// </returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when the provided <paramref name="priority"/> is outside the valid range of priorities.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the specified priority channel is empty and no packet can be dequeued.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public TPacket Dequeue(PacketPriority priority)
    {
        // Check if the priority is valid (ensure it's within the expected range)
        if (priority < 0 || (System.Int32)priority >= this._priorityCount)
        {
            throw new System.ArgumentOutOfRangeException(nameof(priority), "Invalid priority level.");
        }

        // Try to dequeue a packet from the specified priority channel
        return this._priorityChannels[(System.Int32)priority].Reader.TryRead(out TPacket? packet)
            ? packet
            : throw new System.InvalidOperationException("Cannot dequeue from an empty queue.");
    }

    /// <summary>
    /// Attempts to dequeue multiple packets from the specified priority channel up to a given limit.
    /// </summary>
    /// <param name="priority">
    /// The priority level of the packets to dequeue. Must be within the valid range of priorities.
    /// </param>
    /// <param name="limit">
    /// The maximum number of packets to dequeue. If more packets are available, only the first <paramref name="limit"/> packets will be returned.
    /// </param>
    /// <returns>
    /// A list of dequeued packets, up to the specified <paramref name="limit"/>.
    /// </returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when the provided <paramref name="priority"/> is outside the valid range of priorities.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the specified priority channel is empty and no packets can be dequeued.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<TPacket> Dequeue(PacketPriority priority, System.Int32 limit)
    {
        // Check if the priority is valid (ensure it's within the expected range)
        if (priority < 0 || (System.Int32)priority >= this._priorityCount)
        {
            throw new System.ArgumentOutOfRangeException(nameof(priority), "Invalid priority level.");
        }

        System.Collections.Generic.List<TPacket> result = ListPool<TPacket>.Instance.Rent(limit);

        // Try to dequeue packets from the specified priority channel until the limit is reached
        while (result.Count < limit && this._priorityChannels[(System.Int32)priority].Reader.TryRead(out TPacket? packet))
        {
            result.Add(packet);
        }

        return result;
    }

    /// <summary>
    /// Retrieves and removes packets from the queue until the maximum count is reached
    /// or the specified stopping condition returns <c>true</c>.
    /// </summary>
    /// <param name="predicate">
    /// A predicate that determines whether dequeuing should stop. If it returns <c>true</c>,
    /// the method stops dequeuing.
    /// </param>
    /// <param name="limit">The maximum number of packets to dequeue.</param>
    /// <returns>A list of packets dequeued before the stop condition was met.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<TPacket> Dequeue(
        System.Func<TPacket, System.Boolean> predicate, System.Int32 limit = 100)
    {
        System.Collections.Generic.List<TPacket> result = ListPool<TPacket>.Instance.Rent(100);
        System.Int32 count = 0;

        while (count < limit && this.TryDequeue(out TPacket? packet))
        {
            if (!predicate(packet))
            {
                _ = this.Enqueue(packet);
                continue; // Re-enqueue the packet if it doesn't match the predicate
            }

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<TPacket> DequeueBatch(System.Int32 limit = 100)
    {
        System.Collections.Generic.List<TPacket> result = new(System.Math.Min(limit, this._totalCount));

        for (System.Int32 i = 0; i < limit; i++)
        {
            if (!this.TryDequeue(out TPacket? packet))
            {
                break;
            }

            result.Add(packet);
        }

        return result;
    }

    /// <summary>
    /// Attempts to retrieve and remove a packet from the queue in priority order.
    /// </summary>
    /// <param name="packet">The dequeued packet, if available; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a packet was successfully dequeued; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryDequeue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TPacket? packet)
    {
        packet = default;

        if (this._lastSuccessfulPriority >= 0 && this._lastSuccessfulPriority < this._priorityCount)
        {
            if (this._priorityChannels[this._lastSuccessfulPriority].Reader.TryRead(out packet))
            {
                _ = System.Threading.Interlocked.Decrement(ref this._priorityCounts[this._lastSuccessfulPriority]);
                _ = System.Threading.Interlocked.Decrement(ref this._totalCount);

                if (this._options.EnableMetrics)
                {
                    _ = System.Threading.Interlocked.Increment(ref this._dequeuedCounts![this._lastSuccessfulPriority]);
                }
                return true;
            }
        }

        for (System.Int32 i = this._priorityCount - 1; i >= 0; i--)
        {
            if (this._priorityChannels[i].Reader.TryRead(out packet))
            {
                this._lastSuccessfulPriority = i;
                _ = System.Threading.Interlocked.Decrement(ref this._priorityCounts[i]);
                _ = System.Threading.Interlocked.Decrement(ref this._totalCount);

                if (this._options.EnableMetrics)
                {
                    _ = System.Threading.Interlocked.Increment(ref this._dequeuedCounts![i]);
                }
                return true;
            }
        }

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryDequeue(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TPacket? packet,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out TPacket? rejected)
    {
        packet = default;
        rejected = default; // Initialize rejected to default (null)

        for (System.Int32 i = this._priorityCount - 1; i >= 0; i--)
        {
            while (this._priorityChannels[i].Reader.TryRead(out TPacket? temp))
            {
                _ = System.Threading.Interlocked.Decrement(ref this._priorityCounts[i]);
                _ = System.Threading.Interlocked.Decrement(ref this._totalCount);

                System.Boolean isValid = !this._options.EnableValidation || temp.IsValid();
                System.Boolean isExpired = this._options.Timeout != System.TimeSpan.Zero && temp.IsExpired(this._options.Timeout);

                if (!isValid)
                {
                    if (this._options.EnableMetrics)
                    {
                        _ = System.Threading.Interlocked.Increment(ref this._rejectedCounts![i]);
                    }

                    rejected = temp; // Assign the invalid packet
                    temp.Dispose();
                    return false; // Exit with the invalid packet
                }

                if (isExpired)
                {
                    if (this._options.EnableMetrics)
                    {
                        _ = System.Threading.Interlocked.Increment(ref this._expiredCounts![i]);
                    }

                    rejected = temp; // Assign the expired packet
                    temp.Dispose();
                    return false; // Exit with the expired packet
                }

                if (this._options.EnableMetrics)
                {
                    _ = System.Threading.Interlocked.Increment(ref this._dequeuedCounts![i]);
                }

                packet = temp;
                return true;
            }
        }

        rejected = default!;
        return false;
    }

    /// <summary>
    /// Peeks at all packets in the specified priority queue without dequeuing them.
    /// </summary>
    /// <param name="priority">The priority level of the queue to peek from.</param>
    /// <param name="packet">The next packet if available; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a packet was found; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean PeekAll(
        PacketPriority priority,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TPacket? packet)
    {
        System.Threading.Channels.ChannelReader<TPacket> reader = this._priorityChannels[(System.Int32)priority].Reader;

        return reader.TryPeek(out packet);
    }

    /// <summary>
    /// Attempts to peek at the next available packet in the queue without removing it.
    /// Scans from highest to lowest priority.
    /// </summary>
    /// <param name="packet">The next packet if available; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a packet was found; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryPeek([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TPacket? packet)
    {
        // Initialize the output to default before any attempt
        packet = default;

        // Iterate from highest to lowest priority
        for (System.Int32 i = this._priorityCount - 1; i >= 0; i--)
        {
            System.Threading.Channels.ChannelReader<TPacket> reader = this._priorityChannels[i].Reader;

            // Try to peek from the current priority queue
            if (reader.TryPeek(out packet))
            {
                return true; // Return immediately when a packet is found
            }
        }

        return false; // No packets found in any queue
    }
}