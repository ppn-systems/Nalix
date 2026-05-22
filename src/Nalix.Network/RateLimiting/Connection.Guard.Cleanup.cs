// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.RateLimiting;

public sealed partial class ConnectionGuard
{
    #region Initialization

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void INITIALIZE_METRICS()
    {
        _totalRejections = 0;
        _totalCleanedEntries = 0;
        _totalConnectionAttempts = 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SCHEDULE_CLEANUP_JOB()
    {
        TaskManager taskManager = InstanceManager.Instance.GetOrCreateInstance<TaskManager>();

        _ = taskManager.ScheduleRecurring(
            name: TaskNaming.Recurring.CleanupJobId(RecurringName, this.GetHashCode()),
            interval: _cleanupInterval,
            work: _ =>
            {
                this.RUN_CLEANUP_ONCE();
                return ValueTask.CompletedTask;
            },
            options: new RecurringOptions
            {
                NonReentrant = true,
                Tag = TaskNaming.Tags.Service,
                BackoffCap = TimeSpan.FromSeconds(15),
                Jitter = TimeSpan.FromMilliseconds(250),
                ExecutionTimeout = TimeSpan.FromSeconds(2)
            }
        );

        if (_banRepository.IsEnabled)
        {
            _ = taskManager.ScheduleRecurring(
                name: TaskNaming.Recurring.CleanupJobId(RecurringName + ".save", this.GetHashCode()),
                interval: _banRepository.AutoSaveInterval,
                work: _ =>
                {
                    _banRepository.Save(_map);
                    return ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
                    ExecutionTimeout = TimeSpan.FromSeconds(10)
                }
            );
        }
    }

    #endregion Initialization

    #region Cleanup

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RUN_CLEANUP_ONCE()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            DateTime cutoff = Clock.NowUtc() - _inactivityThreshold;
            int scanned = 0;
            int removed = 0;

            int maxCleanupKeys = _config.MaxCleanupKeysPerRun > 0
                ? _config.MaxCleanupKeysPerRun
                : Math.Max(1000, _map.Count / 4);

            List<SocketEndpoint> keysToRemove = new(Math.Min(maxCleanupKeys, _map.Count));

            foreach (KeyValuePair<SocketEndpoint, ConnectionLimitEntry> kvp in _map)
            {
                if (scanned++ >= maxCleanupKeys)
                {
                    break;
                }

                bool lockTaken = false;
                bool shouldRemove = false;

                try
                {
                    kvp.Value.SpinLock.Enter(ref lockTaken);
                    shouldRemove = SHOULD_REMOVE_ENTRY(kvp.Value, cutoff);
                }
                finally
                {
                    if (lockTaken)
                    {
                        kvp.Value.SpinLock.Exit();
                    }
                }

                if (shouldRemove)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            // Remove in separate pass to avoid holding locks
            foreach (SocketEndpoint key in keysToRemove)
            {
                if (_map.TryGetValue(key, out ConnectionLimitEntry? entry) && entry is not null)
                {
                    bool lockTaken = false;
                    bool canRemove = false;
                    try
                    {
                        entry.SpinLock.Enter(ref lockTaken);
                        // Double check under lock to prevent TOCTOU
                        if (SHOULD_REMOVE_ENTRY(entry, cutoff))
                        {
                            entry.IsRemoved = true;
                            canRemove = true;
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            entry.SpinLock.Exit();
                        }
                    }

                    if (canRemove)
                    {
                        if (_map.TryRemove(key, out ConnectionLimitEntry? removedEntry) && removedEntry is not null)
                        {
                            // Clear queue without lock since no one else can acquire it
                            removedEntry.RecentConnectionTimestamps.Clear();
                            removed++;
                            _ = Interlocked.Increment(ref _totalCleanedEntries);
                        }
                    }
                }
            }

            if (removed > 0)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"[NW.{nameof(ConnectionGuard)}] cleanup scanned={scanned} removed={removed} remaining={_map.Count}");
                }
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.{nameof(ConnectionGuard)}] cleanup-error");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SHOULD_REMOVE_ENTRY(ConnectionLimitEntry entry, DateTime cutoff)
    {
        long bannedUntil = Interlocked.Read(ref entry.BannedUntilTicks);
        if (bannedUntil > cutoff.Ticks)
        {
            return false;
        }

        // Read Info without lock — approximate check is fine for cleanup decisions.
        ConnectionLimitInfo info = entry.Info;
        return info.CurrentConnections <= 0 && info.LastConnectionTime < cutoff;
    }

    #endregion Cleanup
}
