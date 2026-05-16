// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking.Sessions;

/// <summary>
/// Defines a contract for creating resumable session entries from active connections.
/// </summary>
public interface ISessionFactory
{
    /// <summary>
    /// Captures the current transport state and attributes of a connection into a resumable <see cref="SessionEntry"/>.
    /// </summary>
    /// <param name="connection">The source connection.</param>
    /// <returns>A new session entry containing the captured snapshot.</returns>
    SessionEntry CreateSession(IConnection connection);
}
