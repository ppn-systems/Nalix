using System.Net;
using FluentAssertions;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;
using Xunit;


#if DEBUG
using System;
using NSubstitute;
using Nalix.Abstractions.Networking;
#endif

namespace Nalix.Network.Tests;

[Collection("NetworkConfigTests")]
public sealed class ConnectionGuardTests
{
    private static IPAddress GetUniqueIp()
    {
        byte[] b = Guid.NewGuid().ToByteArray();
        return new IPAddress(new byte[] { (byte)(b[0] % 223 + 1), b[1], b[2], b[3] });
    }

    [Fact]
    public void TryAccept_WhenUnderLimit_ReturnsTrue()
    {
        ConnectionQuotaOptions options = new() { MaxConnectionsPerIpAddress = 2 };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(GetUniqueIp(), 12345);

        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeTrue();
    }

    [Fact]
    public void TryAccept_WhenOverLimit_ReturnsFalse()
    {
        ConnectionQuotaOptions options = new() { MaxConnectionsPerIpAddress = 1 };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(GetUniqueIp(), 12345);

        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeFalse();
    }

#if DEBUG
    [Fact]
    public void OnConnectionClosed_DecrementsCounter()
    {
        ConnectionQuotaOptions options = new() { MaxConnectionsPerIpAddress = 1 };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(GetUniqueIp(), 12345);

        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeFalse();

        // Simulate connection closed
        IConnectionEventArgs args = Substitute.For<IConnectionEventArgs>();
        args.Connection.NetworkEndpoint.Address.Returns(endpoint.Address.ToString());
        args.Connection.NetworkEndpoint.Returns(Nalix.Network.Internal.Transport.SocketEndpoint.FromIpAddress(endpoint.Address));

        guard.OnConnectionClosed(null, args);

        guard.TryAccept(endpoint).Should().BeTrue();
    }

    [Fact]
    public void TryAccept_WhenBurstTooHigh_BansEndpoint()
    {
        Nalix.Environment.Configuration.ConfigurationManager.Instance.Get<ConnectionGuardOptions>().BanDuration = TimeSpan.FromSeconds(10);
        ConnectionQuotaOptions options = new()
        {
            MaxConnectionsPerWindow = 2,
            ConnectionRateWindow = TimeSpan.FromSeconds(10)
        };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(GetUniqueIp(), 12345);

        // Burst 2 connections
        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeTrue();

        // 3rd connection in window should trigger ban
        guard.TryAccept(endpoint).Should().BeFalse();

        // Even if we release one, it should still be banned
        IConnectionEventArgs args = Substitute.For<IConnectionEventArgs>();
        args.Connection.NetworkEndpoint.Returns(Nalix.Network.Internal.Transport.SocketEndpoint.FromIpAddress(endpoint.Address));
        guard.OnConnectionClosed(null, args);

        guard.TryAccept(endpoint).Should().BeFalse();
    }

    [Fact]
    public void TryAccept_WhenConcurrentLimitReached_AndSpamming_BansEndpoint()
    {
        Nalix.Environment.Configuration.ConfigurationManager.Instance.Get<ConnectionGuardOptions>().BanDuration = TimeSpan.FromSeconds(10);
        ConnectionQuotaOptions options = new()
        {
            MaxConnectionsPerIpAddress = 1,
            MaxConnectionsPerWindow = 2,
            ConnectionRateWindow = TimeSpan.FromSeconds(10)
        };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(GetUniqueIp(), 12345);

        // 1st attempt: Allowed (Current = 1, Limit = 1)
        guard.TryAccept(endpoint).Should().BeTrue();

        // 2nd attempt: Rejected due to concurrent limit (Current = 1, Limit = 1), but enqueued
        guard.TryAccept(endpoint).Should().BeFalse();

        // 3rd attempt: Exceeds rate limit (MaxConnectionsPerWindow = 2), triggers ban
        guard.TryAccept(endpoint).Should().BeFalse();

        // Release the 1st connection
        IConnectionEventArgs args = Substitute.For<IConnectionEventArgs>();
        args.Connection.NetworkEndpoint.Returns(Nalix.Network.Internal.Transport.SocketEndpoint.FromIpAddress(endpoint.Address));
        guard.OnConnectionClosed(null, args);

        // Even though concurrent limit is now 0, the IP should be banned
        guard.TryAccept(endpoint).Should().BeFalse();
    }

    [Fact]
    public void TryAccept_WhenSpammingWhileBanned_EscalatesBanDuration()
    {
        ConnectionQuotaOptions options = new()
        {
            MaxConnectionsPerIpAddress = 10,
            MaxConnectionsPerWindow = 2,
            ConnectionRateWindow = TimeSpan.FromSeconds(5)
        };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(GetUniqueIp(), 12345);

        // 1st & 2nd attempts: Allowed (within limit of 2)
        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeTrue();

        // 3rd attempt: Violates rate limit, triggers 1st ban (Tier 1: 1 minute)
        guard.TryAccept(endpoint).Should().BeFalse();

        // Use Reflection to retrieve the internal ConnectionLimitEntry to verify state
        var mapField = typeof(ConnectionGuard).GetField("_map", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var map = (System.Collections.Concurrent.ConcurrentDictionary<Nalix.Network.Internal.Transport.SocketEndpoint, ConnectionGuard.ConnectionLimitEntry>)mapField!.GetValue(guard)!;
        var key = Nalix.Network.Internal.Transport.SocketEndpoint.FromIpAddress(endpoint.Address);
        
        map.TryGetValue(key, out var entry).Should().BeTrue();
        entry!.BanCount.Should().Be(1);
        long initialBannedUntil = entry.BannedUntilTicks;

        // Simulate continuing to spam while banned.
        // We make more attempts to trigger rate limits while already banned.
        // To bypass the window throttling (which limits escalation to once per window),
        // we manually adjust LastBanTimeTicks backward to simulate elapsed window time.
        entry!.LastBanTimeTicks -= TimeSpan.FromSeconds(6).Ticks;

        guard.TryAccept(endpoint).Should().BeFalse();

        // Check if ban count escalated to 2 (Tier 2: 5 minutes) and BannedUntilTicks is extended
        entry.BanCount.Should().Be(2);
        entry.BannedUntilTicks.Should().BeGreaterThan(initialBannedUntil);
    }
#endif

    [Fact]
    public void TryAccept_WhenIpBlacklistedInFile_ReturnsFalse()
    {
        var blacklistOptions = Nalix.Environment.Configuration.ConfigurationManager.Instance.Get<ConnectionBlacklistStoreOptions>();
        blacklistOptions.Enabled = true;
        blacklistOptions.StoreFileName = "blacklist_test.txt";
        blacklistOptions.MaxBlacklistedIps = 10;

        string path = System.IO.Path.Combine(Nalix.Environment.IO.Directories.ConfigurationDirectory, blacklistOptions.StoreFileName);
        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        System.IO.File.WriteAllLines(path, new[]
        {
            "# Test Blacklist",
            "1.2.3.4",
            "   ",
            "# Another Comment",
            "5.6.7.8"
        });

        try
        {
            ConnectionQuotaOptions quotaOptions = new() { MaxConnectionsPerIpAddress = 10 };
            using ConnectionGuard guard = new(quotaOptions);

            IPEndPoint allowedEndpoint = new(IPAddress.Parse("10.0.0.1"), 80);
            IPEndPoint blacklistedEndpoint1 = new(IPAddress.Parse("1.2.3.4"), 80);
            IPEndPoint blacklistedEndpoint2 = new(IPAddress.Parse("5.6.7.8"), 80);

            guard.TryAccept(allowedEndpoint).Should().BeTrue();
            guard.TryAccept(blacklistedEndpoint1).Should().BeFalse();
            guard.TryAccept(blacklistedEndpoint2).Should().BeFalse();
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    [Fact]
    public void TryAccept_WhenIpInBlacklistedCidr_ReturnsFalse()
    {
        var blacklistOptions = Nalix.Environment.Configuration.ConfigurationManager.Instance.Get<ConnectionBlacklistStoreOptions>();
        blacklistOptions.Enabled = true;
        blacklistOptions.StoreFileName = "blacklist_cidr_test.txt";
        blacklistOptions.MaxBlacklistedIps = 10;

        string path = System.IO.Path.Combine(Nalix.Environment.IO.Directories.ConfigurationDirectory, blacklistOptions.StoreFileName);
        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        System.IO.File.WriteAllLines(path, new[]
        {
            "# Test Blacklist CIDR",
            "192.168.1.0/24",
            "2001:db8::/32"
        });

        try
        {
            ConnectionQuotaOptions quotaOptions = new() { MaxConnectionsPerIpAddress = 10 };
            using ConnectionGuard guard = new(quotaOptions);

            IPEndPoint allowedEndpoint = new(IPAddress.Parse("192.168.2.1"), 80);
            IPEndPoint blacklistedEndpoint1 = new(IPAddress.Parse("192.168.1.5"), 80);
            IPEndPoint blacklistedEndpoint2 = new(IPAddress.Parse("192.168.1.99"), 80);
            IPEndPoint blacklistedIpv6Endpoint = new(IPAddress.Parse("2001:db8::1"), 80);

            guard.TryAccept(allowedEndpoint).Should().BeTrue();
            guard.TryAccept(blacklistedEndpoint1).Should().BeFalse();
            guard.TryAccept(blacklistedEndpoint2).Should().BeFalse();
            guard.TryAccept(blacklistedIpv6Endpoint).Should().BeFalse();
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    [Fact]
    public void TryAccept_WhenIpInTrustedProxiesFile_BypassesStandardCeilings()
    {
        var proxyOptions = Nalix.Environment.Configuration.ConfigurationManager.Instance.Get<TrustedProxyOptions>();
        proxyOptions.StoreFileName = "trusted_proxies_test.txt";
        proxyOptions.MaxTrustedProxies = 10;
        proxyOptions.MaxConnectionsPerTrustedProxy = 3;

        string path = System.IO.Path.Combine(Nalix.Environment.IO.Directories.ConfigurationDirectory, proxyOptions.StoreFileName);
        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        System.IO.File.WriteAllLines(path, new[]
        {
            "# Test Trusted Proxies",
            "10.0.0.0/24",
            "192.168.100.5"
        });

        try
        {
            ConnectionQuotaOptions quotaOptions = new()
            {
                MaxConnectionsPerIpAddress = 1,
                MaxConnectionsPerWindow = 100
            };
            using ConnectionGuard guard = new(quotaOptions);

            IPEndPoint normalEndpoint = new(IPAddress.Parse("1.2.3.4"), 80);
            IPEndPoint trustedSubnetEndpoint = new(IPAddress.Parse("10.0.0.50"), 80);
            IPEndPoint trustedIpEndpoint = new(IPAddress.Parse("192.168.100.5"), 80);

            // Normal endpoint: limit is 1
            guard.TryAccept(normalEndpoint).Should().BeTrue();
            guard.TryAccept(normalEndpoint).Should().BeFalse();

            // Trusted subnet endpoint: limit is 3 (MaxConnectionsPerTrustedProxy)
            guard.TryAccept(trustedSubnetEndpoint).Should().BeTrue();
            guard.TryAccept(trustedSubnetEndpoint).Should().BeTrue();
            guard.TryAccept(trustedSubnetEndpoint).Should().BeTrue();
            guard.TryAccept(trustedSubnetEndpoint).Should().BeFalse();

            // Trusted specific IP endpoint: limit is 3 (MaxConnectionsPerTrustedProxy)
            guard.TryAccept(trustedIpEndpoint).Should().BeTrue();
            guard.TryAccept(trustedIpEndpoint).Should().BeTrue();
            guard.TryAccept(trustedIpEndpoint).Should().BeTrue();
            guard.TryAccept(trustedIpEndpoint).Should().BeFalse();
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    [Fact]
    public void CurrentDifficulty_WhenAdaptiveModeEnabled_ScalesBasedOnEwmaRate()
    {
        ConnectionQuotaOptions options = new()
        {
            EnableAdaptiveMode = true,
            AdaptivePowMinDifficulty = 12,
            AdaptivePowMaxDifficulty = 24,
            AdaptivePowStartRate = 10,
            AdaptivePowMaxRate = 100,
            EnableCapacityBasedPoW = false
        };
        using ConnectionGuard guard = new(options);

        // Access internal field _ewmaConnectionRate to simulate rate changes
        var ewmaField = typeof(ConnectionGuard).GetField("_ewmaConnectionRate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Test Min
        ewmaField!.SetValue(guard, 5.0);
        guard.CurrentDifficulty.Should().Be(12);

        // Test Max
        ewmaField.SetValue(guard, 150.0);
        guard.CurrentDifficulty.Should().Be(24);

        // Test Scaling (Midpoint)
        ewmaField.SetValue(guard, 55.0); // (55-10)/90 = 45/90 = 0.5. diff = 12 + 0.5*12 = 18
        guard.CurrentDifficulty.Should().Be(18);
    }

    [Fact]
    public void CurrentDifficulty_WhenAdaptiveModeDisabled_ReturnsMinDifficulty()
    {
        ConnectionQuotaOptions options = new()
        {
            EnableAdaptiveMode = false,
            AdaptivePowMinDifficulty = 15,
            AdaptivePowMaxDifficulty = 24
        };
        using ConnectionGuard guard = new(options);

        var ewmaField = typeof(ConnectionGuard).GetField("_ewmaConnectionRate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ewmaField!.SetValue(guard, 200.0); // Rate is high, but adaptive is disabled

        guard.CurrentDifficulty.Should().Be(15);
    }

    [Fact]
    public void VerifyPowLogic()
    {
        (var nonce, var mac) = Nalix.Codec.Security.ProofOfWork.CreateChallenge(12, 822522449546468353, 189525343);
        long solution = Nalix.Codec.Security.ProofOfWorkSolver.SolveChallenge(nonce.AsSpan(), 12, 189525343);
        bool verifyResult = Nalix.Codec.Security.ProofOfWork.VerifySolution(nonce.AsSpan(), 12, 189525343, 822522449546468353, mac.AsSpan(), solution);
        verifyResult.Should().BeTrue();
    }
}
