// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Concurrency;
using Nalix.Common.Identity;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;

namespace Nalix.Network.Sessions;

/// <summary>
/// An in-memory implementation of <see cref="ISessionStore"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for single-node deployments. For distributed scenarios, replace with a Redis-backed store.
/// </summary>
public sealed class InMemorySessionStore : SessionStoreBase, IDisposable
{
    private readonly ConcurrentDictionary<UInt56, SessionEntry> _store = new();
    private readonly IWorkerHandle _scavenger;
    private readonly CancellationTokenSource _cts = new();
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySessionStore"/> class
    /// and starts the background scavenger for cleaning up expired sessions.
    /// </summary>
    public InMemorySessionStore()
    {
        _scavenger = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{TaskNaming.Tags.Service}.{TaskNaming.Tags.Cleanup}.sessions",
            group: TaskNaming.Tags.Cleanup,
            work: this.SCAVENGE_LOOP,
            options: new WorkerOptions
            {
                Tag = TaskNaming.Tags.Cleanup,
                IdType = SnowflakeType.System,
                CancellationToken = _cts.Token,
                RetainFor = TimeSpan.Zero
            }
        );
    }

    private async ValueTask SCAVENGE_LOOP(IWorkerContext ctx, CancellationToken ct)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            ctx.Beat();
            try
            {
                this.Scavenge();
            }
            catch
            {
                // Ignore background cleanup errors
            }
        }
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

        UInt56 token = entry.Snapshot.SessionToken;

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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        catch
        {
            // Ignore cancel errors
        }

        try
        {
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelWorker(_scavenger.Id);
            _scavenger.Dispose();
        }
        catch
        {
            // Best-effort cleanup
        }

        GC.SuppressFinalize(this);
    }
}
