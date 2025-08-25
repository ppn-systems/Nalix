// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Throttling.Metadata;

/// <summary>
/// Stores connection tracking data for an IP address.
/// Optimized as a readonly record struct for performance and memory usage.
/// </summary>
[System.Diagnostics.DebuggerDisplay(
    "Current: {CurrentConnections}, Total: {TotalConnectionsToday}, Last: {LastConnectionTime}")]
internal readonly record struct ConnectionLimitInfo
{
    /// <summary>
    /// The current TransportProtocol of active connections.
    /// </summary>
    public readonly System.Int32 CurrentConnections { get; init; }

    /// <summary>
    /// When the most recent connection was established.
    /// </summary>
    public readonly System.DateTime LastConnectionTime { get; init; }

    /// <summary>
    /// The total TransportProtocol of connections made today.
    /// </summary>
    public readonly System.Int32 TotalConnectionsToday { get; init; }

    /// <summary>
    /// When the last cleanup operation was performed.
    /// </summary>
    public readonly System.DateTime LastCleanupTime { get; init; }

    /// <summary>
    /// Creates a new connection info record.
    /// </summary>
    public ConnectionLimitInfo(
        System.Int32 currentConnections,
        System.DateTime lastConnectionTime,
        System.Int32 totalConnectionsToday,
        System.DateTime lastCleanupTime)
    {
        this.CurrentConnections = currentConnections;
        this.LastConnectionTime = lastConnectionTime;
        this.TotalConnectionsToday = totalConnectionsToday;
        this.LastCleanupTime = lastCleanupTime;
    }
}
