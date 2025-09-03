using Nalix.Network.Configurations;
using Nalix.Network.Throttling;
using System;
using System.Net;
using Xunit;

namespace Nalix.Network.Tests.Throttling;

/// <summary>
/// Unit tests for <see cref="ConnectionLimiter"/>.
/// </summary>
public sealed class ConnectionLimiterTests : IDisposable
{
    private readonly ConnectionLimiter _limiter;

    public ConnectionLimiterTests()
    {
        var options = new ConnLimitOptions
        {
            MaxConnectionsPerIpAddress = 2,
            InactivityThresholdMs = 5000,
            CleanupIntervalMs = 1000,
        };
        _limiter = new ConnectionLimiter(options);
    }

    [Fact]
    public void IsConnectionAllowed_ShouldAllowUpToLimit_ThenBlock()
    {
        // Arrange
        var ip = IPAddress.Loopback;

        // Act + Assert
        Assert.True(_limiter.IsConnectionAllowed(ip));
        Assert.True(_limiter.IsConnectionAllowed(ip));
        Assert.False(_limiter.IsConnectionAllowed(ip)); // third should be blocked
    }

    [Fact]
    public void ConnectionClosed_ShouldDecrementCounters()
    {
        var ip = IPAddress.Loopback;

        _ = _limiter.IsConnectionAllowed(ip);
        _ = _limiter.IsConnectionAllowed(ip);
        var (CurrentConnections, _, _) = _limiter.GetConnectionInfo(ip);
        Assert.Equal(2, CurrentConnections);

        Assert.True(_limiter.ConnectionClosed(ip));
        var (Current, _, _) = _limiter.GetConnectionInfo(ip);
        Assert.Equal(1, Current);
    }

    public void Dispose() => _limiter.Dispose();
}
