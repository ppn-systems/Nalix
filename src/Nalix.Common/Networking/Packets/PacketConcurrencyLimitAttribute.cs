// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Limit how many requests can run concurrently for a handler.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PacketConcurrencyLimitAttribute(
    int max, bool queue = false, int queueMax = 0) : System.Attribute
{
    /// <summary>
    /// Maximum concurrent executions allowed.
    /// </summary>
    public int Max { get; } = max;

    /// <summary>
    /// If true, enqueue instead of rejecting when full.
    /// </summary>
    public bool Queue { get; } = queue;

    /// <summary>
    /// Maximum queue length (0 = no queue, reject when full).
    /// </summary>
    public int QueueMax { get; } = queueMax;
}