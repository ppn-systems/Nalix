// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySessionStore"/> class
    /// and starts the background scavenger for cleaning up expired sessions.
    /// </summary>
    public InMemorySessionStore()
    {
        _ = Task.Run(async () =>
        {
            using PeriodicTimer timer = new(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                try
                {
                    this.Scavenge();
                }
                catch
                {
                    // Ignore background cleanup errors
                }
            }
        });
    }

    private void Scavenge()
    {
        long now = Clock.UnixMillisecondsNow();
        foreach (KeyValuePair<UInt56, SessionEntry> pair in _store)
        {
            if (pair.Value.Snapshot.ExpiresAtUnixMilliseconds <= now)
            {
                if (_store.TryRemove(pair.Key, out SessionEntry? expired))
                {
                    expired.Return();
                }
            }
        }
    }

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

    /// <inheritdoc />
    /// <remarks>
    /// SEC-33 fix: Uses <c>ConcurrentDictionary.TryRemove</c> for atomic
    /// retrieve-and-remove. Only one concurrent caller can successfully consume a given token.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask<SessionEntry?> ConsumeAsync(UInt56 sessionToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_store.TryRemove(sessionToken, out SessionEntry? entry))
        {
            return ValueTask.FromResult<SessionEntry?>(null);
        }

        // Check TTL — if expired, return the entry resources and report null.
        if (entry.Snapshot.ExpiresAtUnixMilliseconds <= Clock.UnixMillisecondsNow())
        {
            entry.Return();
            return ValueTask.FromResult<SessionEntry?>(null);
        }

        return ValueTask.FromResult<SessionEntry?>(entry);
    }
}
