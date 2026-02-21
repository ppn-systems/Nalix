namespace Nalix.Common.Enums;

/// <summary>
/// Behavior when a per-connection queue is full.
/// </summary>
public enum DropPolicy
{
    /// <summary>
    /// Drop the incoming (newest) packet.
    /// </summary>
    DROP_NEWEST = 0,

    /// <summary>
    /// Drop the oldest packet in the queue to make room for the new one.
    /// </summary>
    DROP_OLDEST = 1,

    /// <summary>
    /// BLOCK the producer until there is room (backpressure).
    /// WARNING: may stall the receiving loop if abused.
    /// </summary>
    BLOCK = 2,

    /// <summary>
    /// COALESCE duplicate packets (by key) and keep only the latest.
    /// </summary>
    COALESCE = 3
}
