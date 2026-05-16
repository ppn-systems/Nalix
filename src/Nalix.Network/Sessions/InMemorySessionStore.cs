// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Network.Sessions;

/// <summary>
/// An in-memory implementation of <see cref="ISessionStore"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for single-node deployments. For distributed scenarios, replace with a Redis-backed store.
/// </summary>
public sealed class InMemorySessionStore : SessionStoreBase, IDisposable
{
    private int _disposed;

    private readonly IWorkerHandle _scavenger;
    private readonly ConcurrentDictionary<ulong, SessionEntry> _store = new();

    /// <inheritdoc/>
    public override ISessionFactory Factory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySessionStore"/> class.
    /// </summary>
    public InMemorySessionStore()
    {
        this.Factory = new SessionFactory();

        _scavenger = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{TaskNaming.Tags.Service}.{TaskNaming.Tags.Cleanup}.sessions",
            group: TaskNaming.Tags.Cleanup,
            work: this.ExecuteAsync,
            options: new WorkerOptions
            {
                RetainFor = TimeSpan.Zero,
                Tag = TaskNaming.Tags.Cleanup,
                IdType = SnowflakeType.System
            }
        );
    }

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
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelWorker(_scavenger.Id);
            _scavenger.Dispose();
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            // Best-effort cleanup
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Scans the store and removes expired sessions.
    /// This method is intended to be called by an external manager or scavenger.
    /// </summary>
    /// <param name="ct">A cancellation token to stop the operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal async ValueTask ScavengeAsync(CancellationToken ct)
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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask StoreAsync(SessionEntry entry, CancellationToken cancellationToken = default)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask RemoveAsync(ulong sessionToken, CancellationToken cancellationToken = default)
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
    public override ValueTask<SessionEntry?> RetrieveAsync(ulong sessionToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_store.TryGetValue(sessionToken, out SessionEntry? entry))
        {
            return ValueTask.FromResult<SessionEntry?>(null);
        }

        // Lazy expiration — check TTL immediately upon retrieval
        if (entry.Snapshot.ExpiresAtUnixMilliseconds <= Clock.UnixMillisecondsNow())
        {
            // Exact reference removal to prevent race condition deleting a newer session on same ID
            if (((ICollection<KeyValuePair<ulong, SessionEntry>>)_store).Remove(new KeyValuePair<ulong, SessionEntry>(sessionToken, entry)))
            {
                entry.Return();
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
    public override ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, CancellationToken cancellationToken = default)
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
