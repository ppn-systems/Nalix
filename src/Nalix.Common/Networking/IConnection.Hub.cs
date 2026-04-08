// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using Nalix.Common.Identity;

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
    /// Attempts to register a connection without throwing on expected contention paths.
    /// </summary>
    /// <param name="connection">The client connection to register.</param>
    /// <returns><c>true</c> when the connection was registered; otherwise <c>false</c>.</returns>
    bool TryRegisterConnection(IConnection connection);

    /// <summary>
    /// Unregisters a client connection from the session manager using its unique identifier.
    /// </summary>
    /// <param name="connection">The connection to unregister.</param>
    void UnregisterConnection(IConnection connection);

    /// <summary>
    /// Attempts to unregister a connection without throwing when it is already absent.
    /// </summary>
    /// <param name="connection">The connection to unregister.</param>
    /// <returns><c>true</c> when the connection was removed; otherwise <c>false</c>.</returns>
    bool TryUnregisterConnection(IConnection connection);

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
}
