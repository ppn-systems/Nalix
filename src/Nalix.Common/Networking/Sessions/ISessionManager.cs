// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Primitives;

namespace Nalix.Common.Networking.Sessions;

/// <summary>
/// Defines session snapshot storage and resume coordination for transport reconnect flows.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Captures the current connection state into a resumable snapshot.
    /// </summary>
    /// <param name="connection">The authenticated connection to snapshot.</param>
    /// <returns>The created snapshot.</returns>
    SessionSnapshot CreateSnapshot(IConnection connection);

    /// <summary>
    /// Attempts to resume a session token onto a new connection.
    /// </summary>
    /// <param name="sessionToken">The token presented by the reconnecting client.</param>
    /// <param name="connection">The new connection that should receive restored state.</param>
    /// <returns>The resume result.</returns>
    SessionResumeResult TryResume(UInt56 sessionToken, IConnection connection);

    /// <summary>
    /// Resolves the active connection for a token if the token is still bound.
    /// </summary>
    /// <param name="sessionToken">The token to resolve.</param>
    /// <param name="connection">The resolved connection when found.</param>
    /// <returns><see langword="true"/> if a live connection is bound to the token.</returns>
    bool TryGetActiveConnection(UInt56 sessionToken, [NotNullWhen(true)] out IConnection? connection);

    /// <summary>
    /// Reads a snapshot without consuming it.
    /// </summary>
    /// <param name="sessionToken">The token to inspect.</param>
    /// <param name="snapshot">The snapshot when present.</param>
    /// <returns><see langword="true"/> when a live snapshot exists.</returns>
    bool TryGetSnapshot(UInt56 sessionToken, [NotNullWhen(true)] out SessionSnapshot? snapshot);
}
