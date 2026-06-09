using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Network.Options;
using Nalix.Runtime.Options;
using Nalix.Runtime.Throttling;

namespace Nalix.Network.Tests;

public sealed class NetworkSmokeTests
{
    [Fact]
    public void Validate_ConnectionQuotaOptions_DefaultsAreValid()
    {
        ConnectionQuotaOptions options = new();

        options.Validate();

        Assert.Equal(10, options.MaxConnectionsPerIpAddress);
        Assert.Equal(10, options.MaxConnectionsPerWindow);
    }

    [Fact]
    public void Validate_TokenBucketOptions_NonPowerOfTwoShardCount_ThrowsValidationException()
    {
        TokenBucketOptions options = new()
        {
            ShardCount = 3
        };

        _ = Assert.Throws<ValidationException>(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ConnectionGuardOptions_InvalidMaxPacketPerSecond_ThrowsValidationException(int value)
    {
        ConnectionGuardOptions options = new()
        {
            MaxPacketPerSecond = value
        };

        _ = Assert.Throws<ValidationException>(() => options.Validate());
    }

    [Fact]
    public void Evaluate_DisposedLimiter_ReturnsHardLockoutDecision()
    {
        TokenBucketOptions options = new()
        {
            CapacityTokens = 2,
            RefillTokensPerSecond = 1,
            CleanupIntervalSeconds = 60,
            StaleEntrySeconds = 60,
            ShardCount = 2,
            MaxTrackedEndpoints = 8
        };

        TokenBucketLimiter limiter = new(options);
        limiter.Dispose();

        TokenBucketLimiter.RateLimitDecision decision = limiter.Evaluate(new TestEndpoint("127.0.0.1"));

        Assert.False(decision.Allowed);
        Assert.Equal(TokenBucketLimiter.RateLimitReason.HardLockout, decision.Reason);
    }

    [Fact]
    public void NetworkEndpointInterfaceProperties_RemainAccessibleThroughInterface()
    {
        INetworkEndpoint endpoint = new TestEndpoint("10.0.0.5", 27015, HasPort: true, IsIPv6: false);

        Assert.Equal("10.0.0.5", endpoint.Address);
        Assert.Equal(27015, endpoint.Port);
        Assert.True(endpoint.HasPort);
        Assert.False(endpoint.IsIPv6);
    }

    private sealed record TestEndpoint(string Address, int Port = 0, bool HasPort = false, bool IsIPv6 = false) : INetworkEndpoint
    {
    }
}
