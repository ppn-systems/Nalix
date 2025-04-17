namespace Notio.Network.Dispatcher.Queue.Statistics;

/// <summary>
/// Represents statistical information about the state and usage of a priority packet queue.
/// </summary>
public sealed class PriorityStatistics
{
    /// <summary>
    /// Gets the current number of packets in the queue.
    /// </summary>
    public int CurrentQueueSize { get; init; }

    /// <summary>
    /// Gets the total number of packets that have been enqueued since the queue was created.
    /// </summary>
    public int EnqueuedCount { get; init; }

    /// <summary>
    /// Gets the total number of packets that have been dequeued and processed.
    /// </summary>
    public int DequeuedCount { get; init; }

    /// <summary>
    /// Gets the total number of packets that have expired and were removed from the queue without processing.
    /// </summary>
    public int ExpiredCount { get; init; }

    /// <summary>
    /// Gets the total number of packets that were rejected or identified as invalid upon enqueue.
    /// </summary>
    public int InvalidCount { get; init; }
}
