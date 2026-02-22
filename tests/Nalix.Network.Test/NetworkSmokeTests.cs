using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Nalix.Common.Networking;
using Nalix.Network.Options;
using Nalix.Network.Pipeline.Options;
using Nalix.Network.Pipeline.Throttling;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class NetworkSmokeTests
{
    [Fact]
    public void Validate_ConnectionLimitOptions_DefaultsAreValid()
    {
        ConnectionLimitOptions options = new();

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
    public void GetReportData_AfterEvaluation_ContainsTrackedEndpointSummary()
    {
        TokenBucketOptions options = new()
        {
            CapacityTokens = 3,
            RefillTokensPerSecond = 1,
            CleanupIntervalSeconds = 60,
            StaleEntrySeconds = 60,
            ShardCount = 2,
            MaxTrackedEndpoints = 8
        };

        using TokenBucketLimiter limiter = new(options);

        _ = limiter.Evaluate(new TestEndpoint("192.168.1.10"));

        IDictionary<string, object> report = limiter.GetReportData();

        Assert.Equal(3, report["CapacityTokens"]);
        Assert.Equal(1, report["TrackedEndpoints"]);
        Assert.True(report.ContainsKey("Endpoints"));
    }

    private sealed record TestEndpoint(string Address) : INetworkEndpoint;
}
