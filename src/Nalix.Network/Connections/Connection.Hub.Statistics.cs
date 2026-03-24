// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security;

namespace Nalix.Network.Connections;

/// <summary>
/// Provides diagnostic statistics for a <see cref="ConnectionHub"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConnectionHubStatistics"/> struct.
/// </remarks>
public readonly struct ConnectionHubStatistics(
    System.Int32 connectionCount,
    System.Int32 maxConnections,
    DropPolicy dropPolicy,
    System.Int32 shardCount,
    System.Int32 anonymousQueueDepth,
    System.Int32 evictedConnections,
    System.Int32 rejectedConnections)
{

    /// <summary>
    /// Gets the current number of registered connections.
    /// </summary>
    public System.Int32 ConnectionCount { get; } = connectionCount;

    /// <summary>
    /// Gets the configured maximum number of connections.
    /// </summary>
    public System.Int32 MaxConnections { get; } = maxConnections;

    /// <summary>
    /// Gets the drop policy that is active when limits are reached.
    /// </summary>
    public DropPolicy DropPolicy { get; } = dropPolicy;

    /// <summary>
    /// Gets the number of shards used for connection storage.
    /// </summary>
    public System.Int32 ShardCount { get; } = shardCount;

    /// <summary>
    /// Gets the depth of the anonymous eviction queue.
    /// </summary>
    public System.Int32 AnonymousQueueDepth { get; } = anonymousQueueDepth;

    /// <summary>
    /// Gets the cumulative number of evicted connections.
    /// </summary>
    public System.Int32 EvictedConnections { get; } = evictedConnections;

    /// <summary>
    /// Gets the cumulative number of rejected connection attempts.
    /// </summary>
    public System.Int32 RejectedConnections { get; } = rejectedConnections;
}
