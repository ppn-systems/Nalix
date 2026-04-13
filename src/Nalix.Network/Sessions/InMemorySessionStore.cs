// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
using Nalix.Framework.Time;

namespace Nalix.Network.Sessions;

/// <summary>
/// An in-memory implementation of <see cref="ISessionStore"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for single-node deployments. For distributed scenarios, replace with a Redis-backed store.
/// </summary>
public sealed class InMemorySessionStore : SessionStoreBase
{
    private readonly ConcurrentDictionary<UInt56, SessionEntry> _store = new();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask StoreAsync(SessionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        _store[entry.Snapshot.SessionToken] = entry;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask RemoveAsync(UInt56 sessionToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_store.TryRemove(sessionToken, out SessionEntry? entry))
        {
            entry.Return();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask<SessionEntry?> RetrieveAsync(UInt56 sessionToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_store.TryGetValue(sessionToken, out SessionEntry? entry))
        {
            return ValueTask.FromResult<SessionEntry?>(null);
        }

        // Lazy expiration — check TTL immediately upon retrieval because background cleanup is running
        if (entry.Snapshot.ExpiresAtUnixMilliseconds <= Clock.UnixMillisecondsNow())
        {
            if (_store.TryRemove(sessionToken, out SessionEntry? expired))
            {
                expired.Return();
            }

            return ValueTask.FromResult<SessionEntry?>(null);
        }

        return ValueTask.FromResult<SessionEntry?>(entry);
    }
}
