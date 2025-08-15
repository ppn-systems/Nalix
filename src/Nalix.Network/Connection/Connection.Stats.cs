// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Connection;

/// <summary>
/// Connection statistics for monitoring
/// </summary>
public readonly struct ConnectionStats
{
    /// <summary>
    /// The total number of connections.
    /// </summary>
    public System.Int32 TotalConnections { get; init; }

    /// <summary>
    /// The number of anonymous connections.
    /// </summary>
    public System.Int32 AnonymousConnections { get; init; }

    /// <summary>
    /// The number of authenticated connections.
    /// </summary>
    public System.Int32 AuthenticatedConnections { get; init; }
}