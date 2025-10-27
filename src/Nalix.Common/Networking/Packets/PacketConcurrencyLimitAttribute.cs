// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Limit how many requests can run concurrently for a handler.
/// </summary>
/// <param name="max">The maximum number of concurrent handler executions allowed.</param>
/// <param name="queue">Whether excess requests should be queued instead of rejected.</param>
/// <param name="queueMax">The maximum queue length allowed when queuing is enabled.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PacketConcurrencyLimitAttribute(
    int max, bool queue = false, int queueMax = 0) : Attribute
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
