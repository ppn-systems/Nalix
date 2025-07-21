namespace Nalix.Network.Snapshot;

/// <summary>
/// Represents statistical information about the state and usage of a priority packet queue.
/// </summary>
public record PriorityQueueStats
{
    /// <summary>
    /// Gets the total number of packets that have been enqueued since the queue was created.
    /// </summary>
    public System.Int32 TotalEnqueued { get; init; }

    /// <summary>
    /// Gets the total number of packets that have been dequeued and processed.
    /// </summary>
    public System.Int32 TotalDequeued { get; init; }

    /// <summary>
    /// Gets the current number of packets in the queue.
    /// </summary>
    public System.Int32 PendingPackets { get; init; }

    /// <summary>
    /// Gets the total number of packets that have expired and were removed from the queue without processing.
    /// </summary>
    public System.Int32 TotalExpiredPackets { get; init; }

    /// <summary>
    /// Gets the total number of packets that were rejected or identified as invalid upon enqueue.
    /// </summary>
    public System.Int32 TotalRejectedPackets { get; init; }
}
