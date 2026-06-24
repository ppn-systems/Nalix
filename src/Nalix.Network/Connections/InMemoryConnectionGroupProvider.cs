// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;

namespace Nalix.Network.Connections;

/// <summary>
/// An in-memory implementation of <see cref="IConnectionGroupRegistry"/> optimized for read-heavy workloads (zero-allocation broadcasting).
/// </summary>
public sealed class InMemoryConnectionGroupProvider : IConnectionGroupRegistry
{
    private readonly ConcurrentDictionary<string, Group> _groups;
    private readonly ConcurrentDictionary<ulong, ConnectionGroups> _connectionGroups;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryConnectionGroupProvider"/> class.
    /// </summary>
    public InMemoryConnectionGroupProvider()
    {
        _groups = new ConcurrentDictionary<string, Group>(StringComparer.Ordinal);
        _connectionGroups = new ConcurrentDictionary<ulong, ConnectionGroups>();
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

        Group group = _groups.GetOrAdd(groupName, static _ => new Group());
        group.Add(connection);

        ConnectionGroups connectionGroups = _connectionGroups.GetOrAdd(connection.ID, static _ => new ConnectionGroups());
        connectionGroups.Add(groupName);

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

        if (_groups.TryGetValue(groupName, out Group? group))
        {
            group.Remove(connection);

            // Note: We avoid aggressively removing the group from the dictionary to prevent
            // race conditions with Add. An empty Group object is extremely lightweight.
        }

        if (_connectionGroups.TryGetValue(connection.ID, out ConnectionGroups? connectionGroups))
        {
            connectionGroups.Remove(groupName);

            if (connectionGroups.IsEmpty)
            {
                _ = _connectionGroups.TryRemove(connection.ID, out _);
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

        if (_connectionGroups.TryRemove(connection.ID, out ConnectionGroups? connectionGroups))
        {
            string[] joinedGroups = connectionGroups.GetGroups();
            foreach (string groupName in joinedGroups)
            {
                if (_groups.TryGetValue(groupName, out Group? group))
                {
                    group.Remove(connection);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IConnection> GetGroupMembers(string groupName)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupName);

        if (_groups.TryGetValue(groupName, out Group? group))
        {
            // O(1) volatile read. Zero allocations.
            return group.Members;
        }

        return Array.Empty<IConnection>();
    }

    private sealed class Group
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ulong, IConnection> _connections = new();

        // Volatile guarantees the latest array reference is read safely by background threads
        private volatile IConnection[] _snapshot = Array.Empty<IConnection>();

        public IConnection[] Members => _snapshot;

        public void Add(IConnection connection)
        {
            lock (_lock)
            {
#if NET8_0_OR_GREATER
                if (_connections.TryAdd(connection.ID, connection))
                {
                    this.UpdateSnapshot();
                }
#else
                if (!_connections.ContainsKey(connection.ID))
                {
                    _connections.Add(connection.ID, connection);
                    UpdateSnapshot();
                }
#endif
            }
        }

        public void Remove(IConnection connection)
        {
            lock (_lock)
            {
                if (_connections.Remove(connection.ID))
                {
                    this.UpdateSnapshot();
                }
            }
        }

        private void UpdateSnapshot()
        {
            if (_connections.Count == 0)
            {
                _snapshot = Array.Empty<IConnection>();
                return;
            }

            IConnection[] newSnapshot = new IConnection[_connections.Count];
            _connections.Values.CopyTo(newSnapshot, 0);
            _snapshot = newSnapshot;
        }
    }

    private sealed class ConnectionGroups
    {
        private readonly Lock _lock = new();
        private readonly HashSet<string> _groups = new(StringComparer.Ordinal);
        private volatile string[] _snapshot = Array.Empty<string>();

        public bool IsEmpty
        {
            get
            {
                lock (_lock)
                {
                    return _groups.Count == 0;
                }
            }
        }

        public void Add(string groupName)
        {
            lock (_lock)
            {
                if (_groups.Add(groupName))
                {
                    this.UpdateSnapshot();
                }
            }
        }

        public void Remove(string groupName)
        {
            lock (_lock)
            {
                if (_groups.Remove(groupName))
                {
                    this.UpdateSnapshot();
                }
            }
        }

        public string[] GetGroups() => _snapshot;

        private void UpdateSnapshot()
        {
            if (_groups.Count == 0)
            {
                _snapshot = Array.Empty<string>();
                return;
            }

            string[] newSnapshot = new string[_groups.Count];
            _groups.CopyTo(newSnapshot, 0);
            _snapshot = newSnapshot;
        }
    }
}
