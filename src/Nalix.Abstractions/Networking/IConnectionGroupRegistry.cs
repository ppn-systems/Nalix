// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Provides a mechanism to manage connection groups for targeted broadcasting.
/// </summary>
public interface IConnectionGroupRegistry
{
    /// <summary>
    /// Adds a connection to the specified group.
    /// </summary>
    /// <param name="groupName">The name of the group.</param>
    /// <param name="connection">The connection to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddToGroupAsync(string groupName, IConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a connection from the specified group.
    /// </summary>
    /// <param name="groupName">The name of the group.</param>
    /// <param name="connection">The connection to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveFromGroupAsync(string groupName, IConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a connection from all groups it currently belongs to.
    /// </summary>
    /// <param name="connection">The connection to remove from all groups.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveFromAllGroupsAsync(IConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a read-only snapshot of all connections currently in the specified group.
    /// </summary>
    /// <param name="groupName">The name of the group.</param>
    /// <returns>A read-only collection of connections in the group.</returns>
    IReadOnlyCollection<IConnection> GetGroupMembers(string groupName);
}
