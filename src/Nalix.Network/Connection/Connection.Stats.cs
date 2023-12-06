// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Connection;

/// <summary>
/// Represents connection statistics for monitoring network activity.
/// </summary>
/// <remarks>
/// This immutable struct provides a snapshot of connection metrics, including total, anonymous, and authenticated connections.
/// </remarks>
public readonly struct ConnectionStats
{
    /// <summary>
    /// Gets the total number of active connections.
    /// </summary>
    public System.Int32 TotalConnections { get; init; }

    /// <summary>
    /// Gets the number of anonymous connections.
    /// </summary>
    public System.Int32 AnonymousConnections { get; init; }

    /// <summary>
    /// Gets the number of authenticated connections.
    /// </summary>
    public System.Int32 AuthenticatedConnections { get; init; }
}