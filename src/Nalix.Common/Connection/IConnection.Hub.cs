// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Security.Abstractions;

namespace Nalix.Common.Connection;

/// <summary>
/// Manages client sessions in a networked application, such as an MMORPG server.
/// Provides methods to register, unregister, retrieve, and close client connections.
/// </summary>
public interface IConnectionHub
{
    /// <summary>
    /// Registers a new client connection to the session manager.
    /// </summary>
    /// <param name="connection">The client connection to register.</param>
    /// <returns><c>true</c> if the connection was successfully registered; otherwise, <c>false</c>.</returns>
    System.Boolean RegisterConnection(IConnection connection);

    /// <summary>
    /// Unregisters a client connection from the session manager using its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the connection to unregister.</param>
    /// <returns><c>true</c> if the connection was successfully unregistered; otherwise, <c>false</c>.</returns>
    System.Boolean UnregisterConnection(IIdentifier id);

    /// <summary>
    /// Associates a username with an existing client connection.
    /// </summary>
    /// <param name="connection">The client connection.</param>
    /// <param name="username">The username to associate.</param>
    /// <remarks>
    /// This method should be called after the connection is successfully registered.
    /// </remarks>
    void AssociateUsername(IConnection connection, System.String username);

    /// <summary>
    /// Closes all active client connections with an optional reason.
    /// </summary>
    /// <param name="reason">The reason for closing connections, if any. Can be <c>null</c>.</param>
    void CloseAllConnections(System.String reason = null);

    /// <summary>
    /// Retrieves a client connection by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the connection to retrieve.</param>
    /// <returns>The <see cref="IConnection"/> if found; otherwise, <c>null</c>.</returns>
    IConnection GetConnection(IIdentifier id);

    /// <summary>
    /// Retrieves a client connection by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the connection to retrieve.</param>
    /// <returns>The <see cref="IConnection"/> if found; otherwise, <c>null</c>.</returns>
    IConnection GetConnection(System.ReadOnlySpan<System.Byte> id);

    /// <summary>
    /// Retrieves a read-only view of all active client connections.
    /// </summary>
    /// <returns>An enumerable collection of all active <see cref="IConnection"/> instances.</returns>
    System.Collections.Generic.IReadOnlyCollection<IConnection> ListConnections();
}