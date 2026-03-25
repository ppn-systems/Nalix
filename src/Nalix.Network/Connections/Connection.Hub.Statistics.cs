// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security;

namespace Nalix.Network.Connections;

/// <summary>
/// Provides diagnostic statistics for a <see cref="ConnectionHub"/>.
/// </summary>
/// <param name="connectionCount"></param>
/// <param name="maxConnections"></param>
/// <param name="dropPolicy"></param>
/// <param name="shardCount"></param>
/// <param name="anonymousQueueDepth"></param>
/// <param name="evictedConnections"></param>
/// <param name="rejectedConnections"></param>
/// <remarks>
/// Initializes a new instance of the <see cref="ConnectionHubStatistics"/> struct.
/// </remarks>
public readonly struct ConnectionHubStatistics(
    int connectionCount,
    int maxConnections,
    DropPolicy dropPolicy,
    int shardCount,
    int anonymousQueueDepth,
    int evictedConnections,
    int rejectedConnections)
{

    /// <summary>
    /// Gets the current number of registered connections.
    /// </summary>
    public int ConnectionCount { get; } = connectionCount;

    /// <summary>
    /// Gets the configured maximum number of connections.
    /// </summary>
    public int MaxConnections { get; } = maxConnections;

    /// <summary>
    /// Gets the drop policy that is active when limits are reached.
    /// </summary>
    public DropPolicy DropPolicy { get; } = dropPolicy;

    /// <summary>
    /// Gets the number of shards used for connection storage.
    /// </summary>
    public int ShardCount { get; } = shardCount;

    /// <summary>
    /// Gets the depth of the anonymous eviction queue.
    /// </summary>
    public int AnonymousQueueDepth { get; } = anonymousQueueDepth;

    /// <summary>
    /// Gets the cumulative number of evicted connections.
    /// </summary>
    public int EvictedConnections { get; } = evictedConnections;

    /// <summary>
    /// Gets the cumulative number of rejected connection attempts.
    /// </summary>
    public int RejectedConnections { get; } = rejectedConnections;
}
