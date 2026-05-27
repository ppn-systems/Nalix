// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Environment.Configuration;
using Nalix.Environment.IO;
using Nalix.Environment.Time;
using Nalix.Network.Internal.Transport;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Security;

/// <summary>
/// Encapsulates the loading, saving, and dirty-state tracking of network bans.
/// </summary>
internal sealed class NetworkBanRepository
{
    private readonly ILogger? _logger;
    private readonly ConnectionBanStoreOptions _storeConfig;
    private readonly string _filePath;
    private int _persistenceDirty;

    public NetworkBanRepository(ILogger? logger = null)
    {
        _logger = logger;
        _storeConfig = ConfigurationManager.Instance.Get<ConnectionBanStoreOptions>();
        _storeConfig.Validate();
        _filePath = Path.Combine(Directories.DataDirectory, _storeConfig.StoreFileName);
    }

    public bool IsEnabled => _storeConfig.Enabled;

    public TimeSpan AutoSaveInterval => _storeConfig.AutoSaveInterval;

    public void MarkDirty() => _ = Interlocked.Exchange(ref _persistenceDirty, 1);

    public void Load(ConcurrentDictionary<SocketEndpoint, ConnectionGuard.ConnectionLimitEntry> map)
    {
        if (!_storeConfig.Enabled)
        {
            return;
        }

        DateTime now = Clock.NowUtc();
        List<NetworkBanRecord> records = NetworkBanStore.Load(_filePath, _storeConfig.MaxPersistedBans, _storeConfig.BanCountDecayWindow, now.Ticks);

        foreach (NetworkBanRecord record in records)
        {
            ConnectionGuard.ConnectionLimitEntry entry = new()
            {
                BannedUntilTicks = record.BannedUntilTicks,
                BanCount = record.BanCount,
                LastBanTimeTicks = record.LastBanTimeTicks,
                LastSeenAtTicks = record.LastSeenAtTicks
            };

            _ = map.TryAdd(SocketEndpoint.FromNetworkEndpoint(record.Endpoint), entry);
        }

        if (records.Count > 0 && _logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation($"[NW.NetworkBanRepository] Loaded {records.Count} persisted bans.");
        }
    }

    public void Save(ConcurrentDictionary<SocketEndpoint, ConnectionGuard.ConnectionLimitEntry> map, bool force = false)
    {
        if (!force && Interlocked.CompareExchange(ref _persistenceDirty, 0, 1) == 0)
        {
            return; // No changes to save
        }

        try
        {
            List<NetworkBanRecord> snapshot = new();
            DateTime now = Clock.NowUtc();

            // Minimal lock to collect snapshot
            foreach (KeyValuePair<SocketEndpoint, ConnectionGuard.ConnectionLimitEntry> kvp in map)
            {
                long bannedUntil = Interlocked.Read(ref kvp.Value.BannedUntilTicks);
                int banCount = kvp.Value.BanCount;

                // Only save entries that are currently banned or have a progressive ban count (to retain decay state)
                if (bannedUntil > now.Ticks || banCount > 0)
                {
                    bool lockTaken = false;
                    long lastBanTime;
                    long lastSeen;

                    try
                    {
                        kvp.Value.SpinLock.Enter(ref lockTaken);
                        lastBanTime = kvp.Value.LastBanTimeTicks;
                        lastSeen = Interlocked.Read(ref kvp.Value.LastSeenAtTicks);
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            kvp.Value.SpinLock.Exit();
                        }
                    }

                    snapshot.Add(new NetworkBanRecord(kvp.Key, bannedUntil, banCount, lastBanTime, lastSeen));
                }
            }

            if (snapshot.Count > 0)
            {
                NetworkBanStore.Save(_filePath, snapshot, snapshot.Count);

                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"[NW.NetworkBanRepository] Persisted {snapshot.Count} bans to disk.");
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            // Restore dirty flag if save failed
            _ = Interlocked.Exchange(ref _persistenceDirty, 1);
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.NetworkBanRepository] failed to save banned ips.");
            }
        }
    }
}
