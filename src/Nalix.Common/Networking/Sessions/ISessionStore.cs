// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Primitives;

namespace Nalix.Common.Networking.Sessions;

/// <summary>
/// Defines a contract for storing, retrieving, and removing connection session entries from a persistent or distributed session store.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Creates a new resumable session for the specified connection.
    /// </summary>
    /// <param name="connection">The connection to create a session for.</param>
    /// <returns>The created session entry.</returns>
    SessionEntry CreateSession(IConnection connection);

    /// <summary>
    /// Persists the specified <see cref="SessionEntry"/> to the store.
    /// </summary>
    /// <param name="entry">The session entry to store.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous store operation.</returns>
    ValueTask StoreAsync(SessionEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a session entry given its session token.
    /// </summary>
    /// <param name="sessionToken">The session token identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> whose result is the <see cref="SessionEntry"/> if found; otherwise, <c>null</c>.
    /// </returns>
    ValueTask<SessionEntry?> RetrieveAsync(UInt56 sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the session entry with the specified session token from the store.
    /// </summary>
    /// <param name="sessionToken">The session token identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous remove operation.</returns>
    ValueTask RemoveAsync(UInt56 sessionToken, CancellationToken cancellationToken = default);
}
