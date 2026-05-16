// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Abstractions.Networking.Sessions;

/// <summary>
/// Defines a contract for storing, retrieving, and removing connection session entries from a persistent or distributed session store.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Defines a contract for creating resumable session entries from active connections.
    /// </summary>
    ISessionFactory Factory { get; }

    /// <summary>
    /// Persists the specified session entry directly to the store.
    /// This is a low-level operation that bypasses connection-level policy checks.
    /// </summary>
    ValueTask StoreAsync(SessionEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a session entry given its session token.
    /// </summary>
    /// <param name="sessionToken">The session token identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> whose result is the <see cref="SessionEntry"/> if found; otherwise, <c>null</c>.
    /// </returns>
    ValueTask<SessionEntry?> RetrieveAsync(ulong sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the session entry with the specified session token from the store.
    /// </summary>
    /// <param name="sessionToken">The session token identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous remove operation.</returns>
    ValueTask RemoveAsync(ulong sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically retrieves and removes a session entry from the store.
    /// This prevents TOCTOU race conditions where two concurrent callers both
    /// successfully retrieve the same token before either removes it (SEC-33).
    /// </summary>
    /// <param name="sessionToken">The session token identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> whose result is the <see cref="SessionEntry"/> if found
    /// and successfully consumed; otherwise, <c>null</c> if the token does not exist or was already consumed.
    /// </returns>
    ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, CancellationToken cancellationToken = default);
}

