namespace Nalix.Network.Dispatch;

/// <summary>
/// Behavior when a per-connection queue is full.
/// </summary>
public enum DropPolicy
{
    /// <summary>Drop the incoming (newest) packet.</summary>
    DropNewest = 0,

    /// <summary>Drop the oldest packet in the queue to make room for the new one.</summary>
    DropOldest = 1,

    /// <summary>
    /// Block the producer until there is room (backpressure).
    /// WARNING: may stall the receiving loop if abused.
    /// </summary>
    Block = 2,

    /// <summary>Coalesce duplicate packets (by key) and keep only the latest.</summary>
    Coalesce = 3
}
