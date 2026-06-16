// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Manages client sessions in a networked application, such as an MMORPG server.
/// Provides methods to register, unregister, retrieve, and close client connections.
/// </summary>
public interface IConnectionHub : IReportable, IDisposable
{
    /// <summary>
    /// Gets the current number of active connections.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Raised after a connection is successfully unregistered.
    /// </summary>
    event Action<IConnection>? ConnectionUnregistered;

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a connection by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the connection to retrieve.</param>
    /// <returns>The connection associated with the identifier, or <c>null</c> if not found.</returns>
    IConnection? GetConnection(ulong id);

    /// <summary>
    /// Retrieves a client connection by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the connection to retrieve.</param>
    /// <returns>The <see cref="IConnection"/> if found; otherwise, <c>null</c>.</returns>
    IConnection? GetConnection(ReadOnlySpan<byte> id);

    /// <summary>
    /// Registers a new client connection to the session manager.
    /// </summary>
    /// <param name="connection">The client connection to register.</param>
    void RegisterConnection(IConnection connection);

    /// <summary>
    /// Unregisters a client connection from the session manager using its unique identifier.
    /// </summary>
    /// <param name="connection">The connection to unregister.</param>
    void UnregisterConnection(IConnection connection);

    /// <summary>
    /// Retrieves a read-only view of all active client connections.
    /// </summary>
    /// <returns>An enumerable collection of all active <see cref="IConnection"/> instances.</returns>
    IReadOnlyCollection<IConnection> ListConnections();

    /// <summary>
    /// Retrieves a read-only view of active client connections from the specified endpoint address.
    /// </summary>
    /// <param name="networkEndpoint">The endpoint address to match.</param>
    /// <returns>An enumerable collection of matching active <see cref="IConnection"/> instances.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="networkEndpoint"/> is null.</exception>
    IReadOnlyCollection<IConnection> ListConnections(INetworkEndpoint networkEndpoint);

    /// <summary>
    /// Broadcasts a message to all active connections using a generic sender, allowing zero-allocation high-performance loops.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the sender.</typeparam>
    /// <typeparam name="TSender">The type of the sender struct implementing <see cref="IConnectionSender{TState}"/>.</typeparam>
    /// <param name="state">The state to pass to the sender.</param>
    /// <param name="sender">The sender struct instance.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous broadcast operation.</returns>
    Task BroadcastAsync<TState, TSender>(TState state, TSender sender, CancellationToken cancellationToken = default)
        where TSender : struct, IConnectionSender<TState>;

    /// <summary>
    /// Multicasts a message to a specific collection of connections using a generic sender, allowing zero-allocation high-performance loops.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the sender.</typeparam>
    /// <typeparam name="TSender">The type of the sender struct implementing <see cref="IConnectionSender{TState}"/>.</typeparam>
    /// <param name="connections">The read-only collection of connections to receive the message.</param>
    /// <param name="state">The state to pass to the sender.</param>
    /// <param name="sender">The sender struct instance.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous multicast operation.</returns>
    Task MulticastAsync<TState, TSender>(IReadOnlyCollection<IConnection> connections, TState state, TSender sender, CancellationToken cancellationToken = default)
        where TSender : struct, IConnectionSender<TState>;
}
