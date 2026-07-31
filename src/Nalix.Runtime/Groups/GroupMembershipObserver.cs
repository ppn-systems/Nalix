// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking;

namespace Nalix.Runtime.Groups;

/// <summary>
/// Observes connection unregistrations and removes the connection from all groups.
/// Decouples the group registry from the connection hub.
/// </summary>
public sealed class GroupMembershipObserver : IDisposable
{
    private bool _disposed;

    private readonly IConnectionHub _hub;
    private readonly IConnectionGroupRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the GroupMembershipObserver and subscribes to hub events.
    /// </summary>
    public GroupMembershipObserver(IConnectionHub hub, IConnectionGroupRegistry registry)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        _hub.ConnectionUnregistered += this.OnConnectionClosed;
    }

    private void OnConnectionClosed(IConnection connection) => _ = _registry.RemoveFromAllGroupsAsync(connection);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _hub.ConnectionUnregistered -= this.OnConnectionClosed;
    }
}
