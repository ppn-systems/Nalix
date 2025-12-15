// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Identity;
using Nalix.Common.Security;

namespace Nalix.Network.Connections;

/// <summary>
/// Event arguments raised when a capacity limit is hit.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConnectionHubEventArgs"/> class.
/// </remarks>
public sealed class ConnectionHubEventArgs(
    DropPolicy dropPolicy,
    System.Int32 currentConnections,
    System.Int32 maxConnections,
    ISnowflake triggeredConnectionId,
    System.String reason,
    ConnectionHubStatistics snapshot) : System.EventArgs
{

    /// <summary>
    /// Gets the active drop policy when the limit fired.
    /// </summary>
    public DropPolicy DropPolicy { get; } = dropPolicy;

    /// <summary>
    /// Gets the number of registered connections when the limit was reached.
    /// </summary>
    public System.Int32 CurrentConnections { get; } = currentConnections;

    /// <summary>
    /// Gets the configured maximum number of connections.
    /// </summary>
    public System.Int32 MaxConnections { get; } = maxConnections;

    /// <summary>
    /// Gets the connection that triggered the limit (may be null if not available).
    /// </summary>
    public ISnowflake TriggeredConnectionId { get; } = triggeredConnectionId;

    /// <summary>
    /// Gets the textual reason for the limit notification.
    /// </summary>
    public System.String Reason { get; } = reason ?? System.String.Empty;

    /// <summary>
    /// Gets a snapshot of hub statistics at the time of the event.
    /// </summary>
    public ConnectionHubStatistics Snapshot { get; } = snapshot;
}
