// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Abstractions.Networking.Sessions;

/// <summary>
/// Defines a service responsible for persisting connection sessions.
/// </summary>
public interface ISessionService : IDisposable
{
    /// <summary>
    /// Attempts to persist the session for the specified connection if it meets the required policies.
    /// </summary>
    /// <param name="connection">The connection to persist.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SaveSessionAsync(IConnection connection, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    ValueTask<SessionScope> ConsumeAsync(ulong sessionToken, Func<SessionEntry, bool>? predicate = null, CancellationToken cancellationToken = default);
}
