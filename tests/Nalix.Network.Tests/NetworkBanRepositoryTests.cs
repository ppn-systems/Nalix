// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using FluentAssertions;
using Nalix.Environment.Configuration;
using Nalix.Environment.IO;
using Nalix.Network.Internal.Security;
using Nalix.Network.Internal.Transport;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class NetworkBanRepositoryTests
{
    [Fact]
    public void Constructor_LoadsOptionsCorrectly()
    {
        var storeConfig = ConfigurationManager.Instance.Get<ConnectionBanStoreOptions>();
        storeConfig.Enabled = true;
        storeConfig.StoreFileName = "bans_test_config.bin";

        NetworkBanRepository repo = new();

        repo.IsEnabled.Should().BeTrue();
        repo.AutoSaveInterval.Should().Be(storeConfig.AutoSaveInterval);
    }

    [Fact]
    public void Load_And_Save_PersistsBansCorrectly()
    {
        var storeConfig = ConfigurationManager.Instance.Get<ConnectionBanStoreOptions>();
        storeConfig.Enabled = true;
        storeConfig.StoreFileName = "bans_test_temp.bin";
        storeConfig.MaxPersistedBans = 100;

        string path = Path.Combine(Directories.DataDirectory, storeConfig.StoreFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        try
        {
            NetworkBanRepository repo = new();

            // Populate some banned IPs in a map
            ConcurrentDictionary<SocketEndpoint, ConnectionGuard.ConnectionLimitEntry> map = new();
            IPEndPoint ep1 = new(IPAddress.Parse("192.168.10.1"), 80);
            IPEndPoint ep2 = new(IPAddress.Parse("192.168.10.2"), 80);

            SocketEndpoint sep1 = SocketEndpoint.FromIpAddress(ep1.Address);
            SocketEndpoint sep2 = SocketEndpoint.FromIpAddress(ep2.Address);

            long futureTime = DateTime.UtcNow.AddMinutes(10).Ticks;

            ConnectionGuard.ConnectionLimitEntry entry1 = new()
            {
                BannedUntilTicks = futureTime,
                BanCount = 3,
                LastBanTimeTicks = DateTime.UtcNow.Ticks,
                LastSeenAtTicks = DateTime.UtcNow.Ticks
            };

            ConnectionGuard.ConnectionLimitEntry entry2 = new()
            {
                BannedUntilTicks = futureTime,
                BanCount = 1,
                LastBanTimeTicks = DateTime.UtcNow.Ticks,
                LastSeenAtTicks = DateTime.UtcNow.Ticks
            };

            map.TryAdd(sep1, entry1);
            map.TryAdd(sep2, entry2);

            // Mark dirty and Save
            repo.MarkDirty();
            repo.Save(map);

            File.Exists(path).Should().BeTrue();

            // Now read it back using a new map
            ConcurrentDictionary<SocketEndpoint, ConnectionGuard.ConnectionLimitEntry> loadedMap = new();
            repo.Load(loadedMap);

            loadedMap.Count.Should().Be(2);
            loadedMap.TryGetValue(sep1, out var loadedEntry1).Should().BeTrue();
            loadedEntry1!.BanCount.Should().Be(3);
            loadedEntry1.BannedUntilTicks.Should().Be(entry1.BannedUntilTicks);

            loadedMap.TryGetValue(sep2, out var loadedEntry2).Should().BeTrue();
            loadedEntry2!.BanCount.Should().Be(1);
            loadedEntry2.BannedUntilTicks.Should().Be(entry2.BannedUntilTicks);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Save_DoesNotWriteFile_WhenNotDirtyAndNotForced()
    {
        var storeConfig = ConfigurationManager.Instance.Get<ConnectionBanStoreOptions>();
        storeConfig.Enabled = true;
        storeConfig.StoreFileName = "bans_test_dirty.bin";

        string path = Path.Combine(Directories.DataDirectory, storeConfig.StoreFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        try
        {
            NetworkBanRepository repo = new();
            ConcurrentDictionary<SocketEndpoint, ConnectionGuard.ConnectionLimitEntry> map = new();
            IPEndPoint ep = new(IPAddress.Parse("1.2.3.4"), 80);
            SocketEndpoint sep = SocketEndpoint.FromIpAddress(ep.Address);

            ConnectionGuard.ConnectionLimitEntry entry = new()
            {
                BannedUntilTicks = DateTime.UtcNow.AddMinutes(5).Ticks,
                BanCount = 1
            };
            map.TryAdd(sep, entry);

            // Save without marking dirty and without forcing -> Should NOT save
            repo.Save(map, force: false);
            File.Exists(path).Should().BeFalse();

            // Save with force: true -> Should save
            repo.Save(map, force: true);
            File.Exists(path).Should().BeTrue();

            File.Delete(path);

            // Mark dirty -> Should save
            repo.MarkDirty();
            repo.Save(map, force: false);
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
