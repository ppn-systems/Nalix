// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;

namespace Nalix.Runtime.Groups;

/// <summary>
/// An in-memory implementation of <see cref="IConnectionGroupRegistry"/> that uses concurrent collections.
/// </summary>
public sealed class InMemoryGroupStore : IConnectionGroupRegistry
{
    // groupName -> (connectionId -> connection)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ulong, IConnection>> _groups;

    // connectionId -> set of groupNames
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>> _connectionGroups;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryGroupStore"/> class.
    /// </summary>
    public InMemoryGroupStore()
    {
        _connectionGroups = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>>();
        _groups = new ConcurrentDictionary<string, ConcurrentDictionary<ulong, IConnection>>(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public Task AddToGroupAsync(string groupName, IConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupName);
        ArgumentNullException.ThrowIfNull(connection);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        ConcurrentDictionary<ulong, IConnection> groupConnections = _groups.GetOrAdd(
            groupName,
            static _ => new ConcurrentDictionary<ulong, IConnection>());

        groupConnections[connection.ConnectionId] = connection;

        ConcurrentDictionary<string, byte> groupsForConnection = _connectionGroups.GetOrAdd(
            connection.ConnectionId,
            static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));

        groupsForConnection[groupName] = 1;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveFromGroupAsync(string groupName, IConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupName);
        ArgumentNullException.ThrowIfNull(connection);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        if (_groups.TryGetValue(groupName, out ConcurrentDictionary<ulong, IConnection>? groupConnections))
        {
            _ = groupConnections.TryRemove(connection.ConnectionId, out _);

            // Clean up empty groups if desired
            if (groupConnections.IsEmpty)
            {
                _ = _groups.TryRemove(new KeyValuePair<string, ConcurrentDictionary<ulong, IConnection>>(groupName, groupConnections));
            }
        }

        if (_connectionGroups.TryGetValue(connection.ConnectionId, out ConcurrentDictionary<string, byte>? groupsForConnection))
        {
            _ = groupsForConnection.TryRemove(groupName, out _);

            if (groupsForConnection.IsEmpty)
            {
                _ = _connectionGroups.TryRemove(new KeyValuePair<ulong, ConcurrentDictionary<string, byte>>(connection.ConnectionId, groupsForConnection));
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveFromAllGroupsAsync(IConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        if (_connectionGroups.TryRemove(connection.ConnectionId, out ConcurrentDictionary<string, byte>? groupsForConnection))
        {
            foreach (string groupName in groupsForConnection.Keys)
            {
                if (_groups.TryGetValue(groupName, out ConcurrentDictionary<ulong, IConnection>? groupConnections))
                {
                    _ = groupConnections.TryRemove(connection.ConnectionId, out _);

                    if (groupConnections.IsEmpty)
                    {
                        _ = _groups.TryRemove(new KeyValuePair<string, ConcurrentDictionary<ulong, IConnection>>(groupName, groupConnections));
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IConnection> GetGroupMembers(string groupName)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupName);

        if (_groups.TryGetValue(groupName, out ConcurrentDictionary<ulong, IConnection>? groupConnections))
        {
            // Snapshot of the current values
            ICollection<IConnection> values = groupConnections.Values;
            int count = values.Count;
            if (count == 0)
            {
                return Array.Empty<IConnection>();
            }

            IConnection[] members = new IConnection[count];
            values.CopyTo(members, 0);
            return members;
        }

        return Array.Empty<IConnection>();
    }
}
