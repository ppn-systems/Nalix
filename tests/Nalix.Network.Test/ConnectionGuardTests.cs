using System;
using System.Net;
using FluentAssertions;
using Nalix.Common.Networking;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;
using Xunit;
using NSubstitute;

namespace Nalix.Network.Tests;

public sealed class ConnectionGuardTests
{
    [Fact]
    public void TryAccept_WhenUnderLimit_ReturnsTrue()
    {
        ConnectionLimitOptions options = new() { MaxConnectionsPerIpAddress = 2 };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(IPAddress.Parse("1.2.3.4"), 12345);

        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeTrue();
    }

    [Fact]
    public void TryAccept_WhenOverLimit_ReturnsFalse()
    {
        ConnectionLimitOptions options = new() { MaxConnectionsPerIpAddress = 1 };
        using ConnectionGuard guard = new(options);
        IPEndPoint endpoint = new(IPAddress.Parse("1.2.3.4"), 12345);

        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeFalse();
    }

    [Fact]
    public void OnConnectionClosed_DecrementsCounter()
    {
        ConnectionLimitOptions options = new() { MaxConnectionsPerIpAddress = 1 };
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
        ConnectionLimitOptions options = new() 
        { 
            MaxConnectionsPerWindow = 2,
            ConnectionRateWindow = TimeSpan.FromSeconds(10),
            BanDuration = TimeSpan.FromSeconds(10)
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
}
