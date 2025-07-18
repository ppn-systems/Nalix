using Nalix.Common.Package;
using Nalix.Common.Package.Enums;

namespace Nalix.Network.Dispatch.Channel;

internal sealed partial class MultiLevelQueue<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Adds a packet to the appropriate priority queue.
    /// </summary>
    /// <param name="packet">The packet to enqueue.</param>
    /// <returns>
    /// <c>true</c> if the packet was added successfully;
    /// <c>false</c> if the packet is <c>null</c> or the queue has reached its maximum capacity.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool Enqueue(TPacket packet)
    {
        if (packet == null)
            return false;

        int priorityIndex = (int)packet.Priority;

        if (_options.MaxCapacity > 0 &&
            System.Threading.Volatile.Read(ref _totalCount) >= _options.MaxCapacity)
            return false;

        if (_priorityChannels[priorityIndex].Writer.TryWrite(packet))
        {
            System.Threading.Interlocked.Increment(ref _totalCount);
            System.Threading.Interlocked.Increment(ref _priorityCounts[priorityIndex]);

            if (_options.EnableMetrics)
            {
                System.Threading.Interlocked.Increment(ref _enqueuedCounts![priorityIndex]);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to enqueue multiple packets into the queue in order.
    /// Skips any packet that fails to enqueue due to capacity constraints.
    /// </summary>
    /// <param name="packets">The collection of packets to enqueue.</param>
    /// <returns>The total number of packets successfully enqueued.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int Enqueue(System.Collections.Generic.IEnumerable<TPacket> packets)
    {
        int added = 0;

        foreach (TPacket packet in packets)
            if (this.Enqueue(packet)) added++;

        return added;
    }

    /// <summary>
    /// Peeks at the next packet in the specified priority queue without dequeuing it.
    /// </summary>
    /// <param name="priority">The priority level of the queue to peek from.</param>
    /// <returns>
    /// The next packet if available, or <c>null</c> if the queue is empty.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public TPacket? PeekFirst(PacketPriority priority)
        => _priorityChannels[(int)priority].Reader.TryPeek(out TPacket? packet) ? packet : default;

    /// <summary>
    /// Attempts to dequeue a packet from the specified priority queue.
    /// </summary>
    /// <param name="priority">The priority level of the queue to dequeue from.</param>
    /// <param name="packet">The dequeued packet, if successful.</param>
    /// <returns>
    /// <c>true</c> if a packet was dequeued successfully;
    /// <c>false</c> if the queue is empty or the operation failed.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(
        PacketPriority priority,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TPacket? packet)
    {
        packet = default;

        // Check if priority is valid
        if (priority < 0 || (int)priority >= _priorityCount)
            throw new System.ArgumentOutOfRangeException(nameof(priority), "Invalid priority level.");

        return _priorityChannels[(int)priority].Reader.TryRead(out packet);
    }

    /// <summary>
    /// Attempts to enqueue a single packet with multiple retry attempts and an incremental backoff delay.
    /// </summary>
    /// <param name="packet">The packet to enqueue.</param>
    /// <param name="retries">The number of retry attempts before giving up. Default is 3.</param>
    /// <returns>
    /// <c>true</c> if the packet was eventually enqueued within the allowed retries;
    /// <c>false</c> otherwise.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(TPacket packet, int retries = 3)
    {
        for (int i = 0; i <= retries; i++)
        {
            if (this.Enqueue(packet)) return true;
            System.Threading.Thread.SpinWait(20 * (i + 1)); // spin-based delay
        }

        return false;
    }

    /// <summary>
    /// Attempts to re-enqueue a packet, optionally overriding its priority.
    /// This is useful for reprocessing or deferring failed packets.
    /// </summary>
    /// <param name="packet">The packet to re-enqueue.</param>
    /// <param name="priority">
    /// Optional override priority. If not provided, the packet's original priority is used.
    /// </param>
    /// <returns>
    /// <c>true</c> if the packet was successfully re-enqueued;
    /// <c>false</c> if the queue is full.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryRequeue(TPacket packet, PacketPriority? priority = null)
    {
        int priorityIndex = (int)(priority ?? packet.Priority);

        if (priorityIndex < 0 || priorityIndex >= _priorityChannels.Length)
            throw new System.ArgumentOutOfRangeException(nameof(priority), "Invalid priority level.");

        if (_options.MaxCapacity > 0 &&
            System.Threading.Volatile.Read(ref _totalCount) >= _options.MaxCapacity)
            return false;

        if (_priorityChannels[priorityIndex].Writer.TryWrite(packet))
        {
            System.Threading.Interlocked.Increment(ref _totalCount);
            System.Threading.Interlocked.Increment(ref _priorityCounts[priorityIndex]);

            if (_options.EnableMetrics)
            {
                System.Threading.Interlocked.Increment(ref _enqueuedCounts![priorityIndex]);
            }

            return true;
        }

        return false;
    }
}