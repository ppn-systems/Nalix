// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Identity;
using Nalix.Common.Security;

namespace Nalix.Network.Connections;

/// <summary>
/// Event arguments raised when a capacity limit is hit.
/// </summary>
/// <param name="dropPolicy"></param>
/// <param name="currentConnections"></param>
/// <param name="maxConnections"></param>
/// <param name="triggeredConnectionId"></param>
/// <param name="reason"></param>
/// <param name="snapshot"></param>
/// <remarks>
/// Initializes a new instance of the <see cref="ConnectionHubEventArgs"/> class.
/// </remarks>
public sealed class ConnectionHubEventArgs(
    DropPolicy dropPolicy,
    int currentConnections,
    int maxConnections,
    ISnowflake? triggeredConnectionId,
    string reason,
    ConnectionHub.Metrics snapshot) : EventArgs
{

    /// <summary>
    /// Gets the active drop policy when the limit fired.
    /// </summary>
    public DropPolicy DropPolicy { get; } = dropPolicy;

    /// <summary>
    /// Gets the number of registered connections when the limit was reached.
    /// </summary>
    public int CurrentConnections { get; } = currentConnections;

    /// <summary>
    /// Gets the configured maximum number of connections.
    /// </summary>
    public int MaxConnections { get; } = maxConnections;

    /// <summary>
    /// Gets the connection that triggered the limit (may be null if not available).
    /// </summary>
    public ISnowflake? TriggeredConnectionId { get; } = triggeredConnectionId;

    /// <summary>
    /// Gets the textual reason for the limit notification.
    /// </summary>
    public string Reason { get; } = reason ?? string.Empty;

    /// <summary>
    /// Gets a snapshot of hub statistics at the time of the event.
    /// </summary>
    public ConnectionHub.Metrics Snapshot { get; } = snapshot;
}
