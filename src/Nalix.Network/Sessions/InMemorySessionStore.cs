// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Environment.Time;

namespace Nalix.Network.Sessions;

/// <summary>
/// An in-memory implementation of <see cref="ISessionStore"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for single-node deployments. For distributed scenarios, replace with a Redis-backed store.
/// </summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<ulong, SessionEntry> _store = new();

    /// <summary>
    /// Executes the scavenging loop. This method is intended to be called by a <see cref="ITaskManager"/> worker.
    /// </summary>
    /// <param name="ctx">The worker context provided by the task manager.</param>
    /// <param name="ct">A cancellation token to stop the loop.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask ExecuteAsync(IWorkerContext ctx, CancellationToken ct)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            ctx?.Beat();

            try
            {
                await this.ScavengeAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                // Background cleanup errors should not crash the scavenger worker
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

    /// <summary>
    /// Scans the store and removes expired sessions.
    /// This method is intended to be called by an external manager or scavenger.
    /// </summary>
    /// <param name="ct">A cancellation token to stop the operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async ValueTask ScavengeAsync(CancellationToken ct)
    {
        long now = Clock.UnixMillisecondsNow();
        int count = 0;
        foreach (KeyValuePair<ulong, SessionEntry> pair in _store)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (pair.Value.Snapshot.ExpiresAtUnixMilliseconds <= now)
            {
                if (((ICollection<KeyValuePair<ulong, SessionEntry>>)_store).Remove(pair))
                {
                    pair.Value.Return();
                }
            }

            if (++count % 1000 == 0)
            {
                await Task.Yield();
            }
        }
    }

}
