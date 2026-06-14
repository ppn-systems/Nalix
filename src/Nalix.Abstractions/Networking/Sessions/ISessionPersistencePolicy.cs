// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking.Sessions;

/// <summary>
/// Defines a custom policy to determine if a connection's session should be persisted.
/// </summary>
public interface ISessionPersistencePolicy
{
    /// <summary>
    /// Determines whether the session for the specified connection should be persisted.
    /// </summary>
    /// <param name="connection">The connection to evaluate.</param>
    /// <returns><c>true</c> if the session should be persisted; otherwise, <c>false</c>.</returns>
    bool ShouldPersist(IConnection connection);
}
