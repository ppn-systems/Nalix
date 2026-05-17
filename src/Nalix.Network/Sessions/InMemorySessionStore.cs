// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Environment.Time;

namespace Nalix.Network.Sessions;

/// <summary>
/// An in-memory implementation of <see cref="ISessionStore"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for single-node deployments. For distributed scenarios, replace with a Redis-backed store.
/// </summary>
[Worker(
    "service.cleanup.sessions",
    "cleanup",
    Tag = "cleanup", IdType = 1, RetainForMs = 0)]
public sealed class InMemorySessionStore : ISessionStore, IWorker
{
    private readonly ConcurrentDictionary<ulong, SessionEntry> _store = new();

    /// <summary>
    /// Executes the scavenging loop. This method is intended to be called by a <see cref="ITaskManager"/> worker.
    /// </summary>
    public async ValueTask ExecuteAsync(IWorkerContext context, CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            context?.Beat();

            try
            {
                await SCAVENGE_ASYNC(_store, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // Background cleanup errors should not crash the scavenger worker
            }
        }

        static async ValueTask SCAVENGE_ASYNC(ConcurrentDictionary<ulong, SessionEntry> store, CancellationToken cancellationToken)
        {
            long now = Clock.UnixMillisecondsNow();
            int count = 0;

            foreach (KeyValuePair<ulong, SessionEntry> pair in store)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (pair.Value.Snapshot.ExpiresAtUnixMilliseconds <= now &&
                    ((ICollection<KeyValuePair<ulong, SessionEntry>>)store).Remove(pair))
                {
                    pair.Value.Return();
                }

                if (++count % 1000 == 0)
                {
                    await Task.Yield();
                }
            }
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask StoreAsync(SessionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        ulong token = entry.Snapshot.SessionToken;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_store.TryAdd(token, entry))
            {
                return ValueTask.CompletedTask;
            }

            if (!_store.TryGetValue(token, out SessionEntry? current))
            {
                continue;
            }

            // Already stored this exact reference for the token.
            if (ReferenceEquals(current, entry))
            {
                return ValueTask.CompletedTask;
            }

            if (_store.TryUpdate(token, entry, current))
            {
                current.Return();
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// SEC-33 fix: Uses <c>ConcurrentDictionary.TryRemove</c> for atomic
    /// retrieve-and-remove. Only one concurrent caller can successfully consume a given token.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, CancellationToken cancellationToken = default)
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
