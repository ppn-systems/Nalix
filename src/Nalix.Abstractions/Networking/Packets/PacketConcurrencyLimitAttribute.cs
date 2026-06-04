// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Marks a handler with a concurrency limit.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PacketConcurrencyLimitAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of concurrent executions allowed.
    /// </summary>
    public int Max { get; }

    /// <summary>
    /// Gets whether excess requests should be queued instead of rejected.
    /// </summary>
    public bool Queue { get; }

    /// <summary>
    /// Gets the maximum queue length.
    /// </summary>
    public int QueueMax { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketConcurrencyLimitAttribute"/> class.
    /// </summary>
    /// <param name="max">The maximum number of concurrent handler executions allowed.</param>
    /// <param name="queue">Whether excess requests should wait in a queue instead of being rejected.</param>
    /// <param name="queueMax">The maximum queue length when queuing is enabled.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when max is less than or equal to 0 or queueMax is less than 0.</exception>
    public PacketConcurrencyLimitAttribute(int max, bool queue = false, int queueMax = 0)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "Concurrency max must be greater than 0.");
        }

        if (queueMax < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueMax), queueMax, "Queue max cannot be negative.");
        }

        this.Max = max;
        this.Queue = queue;
        this.QueueMax = queueMax;
    }
}
