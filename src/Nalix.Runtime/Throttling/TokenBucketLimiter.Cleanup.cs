// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Pools;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Runtime.Throttling;

public sealed partial class TokenBucketLimiter
{
    #region Cleanup

    /// <summary>
    /// Periodic cleanup of stale endpoints to bound memory use.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CLEANUP_STALE_ENDPOINTS()
    {
        if (_disposed)
        {
            return;
        }

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        CancellationToken token = cts.Token;

        try
        {
            long now = Stopwatch.GetTimestamp();
            int removed = this.PERFORM_STALE_CLEANUP(now, token);

            removed += this.ENFORCE_LIMIT_IF_NEEDED(token);

            if (removed > 0)
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Write(
                        DiagnosticsEvents.Internal.Debug,
                        new DiagnosticLog(
                            "RT.TokenBucketLimiter:Internal",
                            $"cleanup removed={removed}"));
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Warning,
                    new DiagnosticLog(
                        "RT.TokenBucketLimiter:Internal",
                        "cleanup cancelled-due-to-timeout"));
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Error,
                    new DiagnosticLog(
                        "RT.TokenBucketLimiter:Internal",
                        "cleanup-error",
                        ex));
            }
        }
    }

    /// <summary>
    /// Performs cleanup of stale endpoints across all shards.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="token"></param>
    private int PERFORM_STALE_CLEANUP(
        long now,
        CancellationToken token)
    {
        int removed = 0;
        int visited = 0;
        long staleTicks = this.TO_TICKS(_options.StaleEntrySeconds);

        // BUG-25 fix: Rotate shard start index so cleanup doesn't always
        // begin from shard 0. Under flood conditions with a timeout, earlier
        // shards would starve later ones if iteration always starts at 0.
        int shardCount = _shards.Length;
        int startIdx = Interlocked.Increment(ref _cleanupShardStart) % shardCount;

        for (int s = 0; s < shardCount; s++)
        {
            token.ThrowIfCancellationRequested();

            Shard shard = _shards[(startIdx + s) % shardCount];

            foreach (KeyValuePair<INetworkEndpoint, EndpointState> kv in shard.Map)
            {
                visited++;

                if ((visited & (CancellationCheckFrequency - 1)) == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                if (this.TRY_REMOVE_STALE_ENDPOINT(kv, now, staleTicks, shard))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    /// <summary>
    /// Attempts to remove a stale endpoint with double-check pattern.
    /// </summary>
    /// <param name="kv"></param>
    /// <param name="now"></param>
    /// <param name="staleTicks"></param>
    /// <param name="shard"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TRY_REMOVE_STALE_ENDPOINT(
        KeyValuePair<INetworkEndpoint, EndpointState> kv,
        long now, long staleTicks, Shard shard)
    {
        bool shouldRemove;
        EndpointState state = kv.Value;

        if (now - state.LastSeenSw <= staleTicks)
        {
            return false;
        }

        bool lockTaken = false;
        try
        {
            state.SpinLock.Enter(ref lockTaken);
            shouldRemove = (now - state.LastSeenSw) > staleTicks;
        }
        finally
        {
            if (lockTaken)
            {
                state.SpinLock.Exit();
            }
        }

        if (!shouldRemove)
        {
            return false;
        }

        // Only proceed if truly stale
        if (shard.Map.TryRemove(kv.Key, out _))
        {
            _ = Interlocked.Decrement(ref _totalEndpointCount);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Enforces MaxTrackedEndpoints limit if exceeded.
    /// </summary>
    /// <param name="token"></param>
    private int ENFORCE_LIMIT_IF_NEEDED(CancellationToken token)
    {
        if (_options.MaxTrackedEndpoints <= 0)
        {
            return 0;
        }

        int currentCount = Interlocked.CompareExchange(ref _totalEndpointCount, 0, 0);

        if (currentCount <= _options.MaxTrackedEndpoints)
        {
            return 0;
        }

        int toRemove = currentCount - _options.MaxTrackedEndpoints;
        int removed = this.REMOVE_OLDEST_ENDPOINTS(toRemove, token);

        if (removed > 0)
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Warning,
                    new DiagnosticLog(
                        "RT.TokenBucketLimiter:Internal",
                        $"evicted count={removed} limit={_options.MaxTrackedEndpoints}"));
            }
        }

        return removed;
    }

    /// <summary>
    /// Evicts the oldest endpoints across all shards.
    /// </summary>
    /// <param name="count"></param>
    /// <param name="cancellationToken"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int REMOVE_OLDEST_ENDPOINTS(
        int count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return 0;
        }

        List<(INetworkEndpoint Key, long LastSeen)> candidates = this.COLLECT_EVICTION_CANDIDATES(cancellationToken);

        try
        {
            candidates.Sort((a, b) => a.LastSeen.CompareTo(b.LastSeen));
            return this.EVICT_OLDEST_CANDIDATES(candidates, count, cancellationToken);
        }
        finally
        {
            RETURN_EVICTION_CANDIDATES_TO_POOL(candidates);
        }
    }

    /// <summary>
    /// Collects all endpoints as eviction candidates.
    /// </summary>
    /// <param name="cancellationToken"></param>
    private List<(INetworkEndpoint Key, long LastSeen)> COLLECT_EVICTION_CANDIDATES(CancellationToken cancellationToken)
    {
        int estimatedCapacity = Math.Min(
            _totalEndpointCount * 2,
            _options.MaxEvictionCapacity);

        ListPool<(INetworkEndpoint Key, long LastSeen)> pool = ListPool<(INetworkEndpoint Key, long LastSeen)>.Instance;
        List<(INetworkEndpoint Key, long LastSeen)> candidates = pool.Rent(minimumCapacity: estimatedCapacity);

        foreach (Shard shard in _shards)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (KeyValuePair<INetworkEndpoint, EndpointState> kv in shard.Map)
            {
                long lastSeen;
                bool lockTaken = false;

                try
                {
                    kv.Value.SpinLock.Enter(ref lockTaken);
                    lastSeen = kv.Value.LastSeenSw;
                }
                finally
                {
                    if (lockTaken)
                    {
                        kv.Value.SpinLock.Exit();
                    }
                }

                candidates.Add((kv.Key, lastSeen));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Evicts the oldest N candidates.
    /// </summary>
    /// <param name="candidates"></param>
    /// <param name="count"></param>
    /// <param name="cancellationToken"></param>
    private int EVICT_OLDEST_CANDIDATES(
        List<(INetworkEndpoint Key, long LastSeen)> candidates,
        int count,
        CancellationToken cancellationToken)
    {
        int removed = 0;
        int toRemove = Math.Min(count, candidates.Count);

        for (int i = 0; i < toRemove; i++)
        {
            if ((i & (CancellationCheckFrequency - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            INetworkEndpoint endpoint = candidates[i].Key;
            Shard shard = this.SELECT_SHARD(endpoint);

            if (shard.Map.TryRemove(endpoint, out _))
            {
                removed++;
                _ = Interlocked.Decrement(ref _totalEndpointCount);
            }
        }

        return removed;
    }

    /// <summary>
    /// Returns eviction candidates list to pool.
    /// </summary>
    /// <param name="candidates"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RETURN_EVICTION_CANDIDATES_TO_POOL(List<(INetworkEndpoint Key, long LastSeen)> candidates)
    {
        ListPool<(INetworkEndpoint Key, long LastSeen)> pool = ListPool<(INetworkEndpoint Key, long LastSeen)>.Instance;
        pool.Return(candidates, clearItems: true);
    }

    #endregion Cleanup

    /// <summary>
    /// Schedules the recurring cleanup job.
    /// </summary>
    private void SCHEDULE_CLEANUP_JOB()
    {
        _cleanupJob = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: TaskNaming.Recurring.CleanupJobId(RecurringName, this.GetHashCode()),
            interval: TimeSpan.FromSeconds(_cleanupIntervalSec),
            work: _ =>
            {
                this.CLEANUP_STALE_ENDPOINTS();
                return ValueTask.CompletedTask;
            },
            options: new RecurringOptions
            {
                NonReentrant = true,
                Tag = TaskNaming.Tags.Service,
                Jitter = TimeSpan.FromMilliseconds(250),
                ExecutionTimeout = TimeSpan.FromSeconds(2),
                BackoffCap = TimeSpan.FromSeconds(15)
            }
        );
    }
}

