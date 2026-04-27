// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Marks a handler with a concurrency limit.
/// </summary>
/// <param name="max">The maximum number of concurrent handler executions allowed.</param>
/// <param name="queue">Whether excess requests should wait in a queue instead of being rejected.</param>
/// <param name="queueMax">The maximum queue length when queuing is enabled.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PacketConcurrencyLimitAttribute(
    int max, bool queue = false, int queueMax = 0) : Attribute
{
    /// <summary>
    /// Gets the maximum number of concurrent executions allowed.
    /// </summary>
    public int Max { get; } = max;

    /// <summary>
    /// Gets whether excess requests should be queued instead of rejected.
    /// </summary>
    public bool Queue { get; } = queue;

    /// <summary>
    /// Gets the maximum queue length.
    /// </summary>
    public int QueueMax { get; } = queueMax;
}
