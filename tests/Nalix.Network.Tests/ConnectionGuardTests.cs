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

public sealed class ConnectionGuardTests
{
    [Fact]
    public void TryAccept_WhenUnderLimit_ReturnsTrue()
    {
        ConnectionQuotaOptions options = new() { MaxConnectionsPerIpAddress = 2 };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(IPAddress.Parse("1.2.3.4"), 12345);

        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeTrue();
    }

    [Fact]
    public void TryAccept_WhenOverLimit_ReturnsFalse()
    {
        ConnectionQuotaOptions options = new() { MaxConnectionsPerIpAddress = 1 };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(IPAddress.Parse("1.2.3.4"), 12345);

        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeFalse();
    }

#if DEBUG
    [Fact]
    public void OnConnectionClosed_DecrementsCounter()
    {
        ConnectionQuotaOptions options = new() { MaxConnectionsPerIpAddress = 1 };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(IPAddress.Parse("1.2.3.4"), 12345);

        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeFalse();

        // Simulate connection closed
        IConnectEventArgs args = Substitute.For<IConnectEventArgs>();
        args.Connection.NetworkEndpoint.Address.Returns("1.2.3.4");
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
        IPEndPoint endpoint = new(IPAddress.Parse("5.6.7.8"), 12345);

        // Burst 2 connections
        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeTrue();

        // 3rd connection in window should trigger ban
        guard.TryAccept(endpoint).Should().BeFalse();

        // Even if we release one, it should still be banned
        IConnectEventArgs args = Substitute.For<IConnectEventArgs>();
        args.Connection.NetworkEndpoint.Returns(Nalix.Network.Internal.Transport.SocketEndpoint.FromIpAddress(endpoint.Address));
        guard.OnConnectionClosed(null, args);

        guard.TryAccept(endpoint).Should().BeFalse();
    }
#endif

    [Fact]
    public void TryAccept_WhenIpBlacklistedInFile_ReturnsFalse()
    {
        var blacklistOptions = Nalix.Environment.Configuration.ConfigurationManager.Instance.Get<ConnectionBlacklistStoreOptions>();
        blacklistOptions.Enabled = true;
        blacklistOptions.StoreFileName = "blacklist_test.txt";
        blacklistOptions.MaxBlacklistedIps = 10;

        string path = System.IO.Path.Combine(Nalix.Environment.IO.Directories.DataDirectory, blacklistOptions.StoreFileName);
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

        string path = System.IO.Path.Combine(Nalix.Environment.IO.Directories.DataDirectory, blacklistOptions.StoreFileName);
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

        string path = System.IO.Path.Combine(Nalix.Environment.IO.Directories.DataDirectory, proxyOptions.StoreFileName);
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
}
