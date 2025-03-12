// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// Limit how many requests can run concurrently for a handler.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PacketConcurrencyLimitAttribute(
    System.Int32 max, System.Boolean queue = false, System.Int32 queueMax = 0) : System.Attribute
{
    /// <summary>
    /// Maximum concurrent executions allowed.
    /// </summary>
    public System.Int32 Max { get; } = max;

    /// <summary>
    /// If true, enqueue instead of rejecting when full.
    /// </summary>
    public System.Boolean Queue { get; } = queue;

    /// <summary>
    /// Maximum queue length (0 = unbounded or no queue).
    /// </summary>
    public System.Int32 QueueMax { get; } = queueMax;
}
