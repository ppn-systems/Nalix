// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Nalix.Common.Identity;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;

namespace Nalix.Common.Networking;

/// <summary>
/// Manages client sessions in a networked application, such as an MMORPG server.
/// Provides methods to register, unregister, retrieve, and close client connections.
/// </summary>
public interface IConnectionHub
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
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    IConnection? GetConnection(UInt56 id);

    /// <summary>
    /// Retrieves a client connection by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the connection to retrieve.</param>
    /// <returns>The <see cref="IConnection"/> if found; otherwise, <c>null</c>.</returns>
    IConnection? GetConnection(ISnowflake id);

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
    /// Forcibly closes all connections matching the specified IP address.
    /// </summary>
    /// <param name="networkEndpoint">The IP address to forcefully close.</param>
    /// <returns>Number of connections closed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="networkEndpoint"/> is null.</exception>
    int ForceClose(INetworkEndpoint networkEndpoint);

    /// <summary>
    /// Closes all active client connections with an optional reason.
    /// </summary>
    /// <param name="reason">The reason for closing connections, if any. Can be <c>null</c>.</param>
    void CloseAllConnections(string? reason = null);

    /// <summary>
    /// Retrieves a read-only view of all active client connections.
    /// </summary>
    /// <returns>An enumerable collection of all active <see cref="IConnection"/> instances.</returns>
    IReadOnlyCollection<IConnection> ListConnections();

    /// <summary>
    /// Creates a new resumable session for the specified connection.
    /// </summary>
    /// <param name="connection">The connection to create a session for.</param>
    /// <returns>The created session entry.</returns>
    SessionEntry CreateSession(IConnection connection);

    /// <summary>
    /// Attempts to resume a session using a new connection.
    /// </summary>
    /// <param name="sessionToken">The token of the session to resume.</param>
    /// <param name="newConnection">The new connection to bind the session to.</param>
    /// <param name="session">When this method returns, contains the resumed session entry if successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the session was successfully resumed; otherwise, <c>false</c>.</returns>
    bool TryResumeSession(UInt56 sessionToken, IConnection newConnection, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SessionEntry? session);

    /// <summary>
    /// Attempts to resolve an active connection from a session token.
    /// </summary>
    /// <param name="sessionToken">The session token to look up.</param>
    /// <param name="connection">When this method returns, contains the resolved connection if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a matching active connection was found; otherwise, <c>false</c>.</returns>
    bool TryGetActiveConnection(UInt56 sessionToken, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IConnection? connection);
}
