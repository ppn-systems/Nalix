// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Environment.Configuration;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Internal.Transport;
using Nalix.Network.Options;

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

        _cleanupJob = taskManager.ScheduleRecurring(
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
            _saveJob = taskManager.ScheduleRecurring(
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
                    _logger.LogDebug("[NW.ConnectionGuard] cleanup scanned={Scanned} removed={Removed} remaining={MapCount}", scanned, removed, _map.Count);
                }
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "[NW.ConnectionGuard] cleanup-error");
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

    #region Hot Reload

    private DateTime _lastConfigReload = DateTime.MinValue;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void INITIALIZE_HOT_RELOAD()
    {
        TaskManager taskManager = InstanceManager.Instance.GetOrCreateInstance<TaskManager>();

        _hotReloadJob = taskManager.ScheduleRecurring(
            name: TaskNaming.Recurring.CleanupJobId(RecurringName + ".reload", this.GetHashCode()),
            interval: TimeSpan.FromSeconds(60),
            work: _ =>
            {
                this.CHECK_FILE_CHANGES();
                return ValueTask.CompletedTask;
            },
            options: new RecurringOptions
            {
                NonReentrant = true,
                Tag = TaskNaming.Tags.Service,
                Jitter = TimeSpan.FromSeconds(5)
            }
        );

        try
        {
            _configWatcher = new System.IO.FileSystemWatcher(Environment.IO.Directories.ConfigurationDirectory)
            {
                NotifyFilter = System.IO.NotifyFilters.LastWrite,
                Filter = "*.txt",
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += this.OnFileChanged;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "[NW.ConnectionGuard] failed to initialize FileSystemWatcher, relying on 60s periodic polling.");
            }
        }
    }

    private void OnFileChanged(object sender, System.IO.FileSystemEventArgs e)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (e.Name != null && (
            e.Name.Equals(ConfigurationManager.Instance.Get<ConnectionBlacklistStoreOptions>().StoreFileName, StringComparison.OrdinalIgnoreCase) ||
            e.Name.Equals(ConfigurationManager.Instance.Get<TrustedProxyOptions>().StoreFileName, StringComparison.OrdinalIgnoreCase)))
        {
            if (Interlocked.CompareExchange(ref _reloadPending, 1, 0) == 0)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(300).ConfigureAwait(false);
                    try
                    {
                        this.CHECK_FILE_CHANGES();
                    }
                    finally
                    {
                        _ = Interlocked.Exchange(ref _reloadPending, 0);
                    }
                });
            }
        }
    }

    private void CHECK_FILE_CHANGES()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            string blacklistPath = System.IO.Path.Combine(Environment.IO.Directories.ConfigurationDirectory, ConfigurationManager.Instance.Get<ConnectionBlacklistStoreOptions>().StoreFileName);
            string proxiesPath = System.IO.Path.Combine(Environment.IO.Directories.ConfigurationDirectory, ConfigurationManager.Instance.Get<TrustedProxyOptions>().StoreFileName);

            DateTime maxConfigWrite = DateTime.MinValue;

            if (System.IO.File.Exists(blacklistPath))
            {
                DateTime lw = System.IO.File.GetLastWriteTimeUtc(blacklistPath);
                if (lw > maxConfigWrite)
                {
                    maxConfigWrite = lw;
                }
            }
            if (System.IO.File.Exists(proxiesPath))
            {
                DateTime lw = System.IO.File.GetLastWriteTimeUtc(proxiesPath);
                if (lw > maxConfigWrite)
                {
                    maxConfigWrite = lw;
                }
            }

            if (maxConfigWrite > _lastConfigReload)
            {
                _lastConfigReload = maxConfigWrite;
                _accessList.Reload();
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "[NW.ConnectionGuard] error checking file changes for hot reload.");
            }
        }
    }

    #endregion Hot Reload
}
