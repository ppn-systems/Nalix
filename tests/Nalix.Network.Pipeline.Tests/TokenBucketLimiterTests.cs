using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nalix.Common.Networking;
using Nalix.Network.Pipeline.Options;
using Nalix.Network.Pipeline.Throttling;
using Xunit;

namespace Nalix.Network.Pipeline.Tests;

public sealed class TokenBucketLimiterTests
{
    [Fact]
    public void Evaluate_WhenEndpointIsNull_ThrowsArgumentNullException()
    {
        using TokenBucketLimiter limiter = new(CreateOptions());

        _ = Assert.Throws<ArgumentNullException>(() => limiter.Evaluate(null!));
    }

    [Fact]
    public void Evaluate_WhenEndpointAddressIsEmpty_ThrowsArgumentException()
    {
        using TokenBucketLimiter limiter = new(CreateOptions());

        _ = Assert.Throws<ArgumentException>(() => limiter.Evaluate(new TestEndpoint("")));
    }

    [Fact]
    public void Evaluate_WhenBucketIsExhausted_ReturnsSoftThrottle()
    {
        TokenBucketOptions options = CreateOptions();
        options.CapacityTokens = 1;
        options.RefillTokensPerSecond = 0.001;
        options.MaxSoftViolations = 3;

        using TokenBucketLimiter limiter = new(options);
        TestEndpoint endpoint = new("127.0.0.10");

        TokenBucketLimiter.RateLimitDecision first = limiter.Evaluate(endpoint);
        TokenBucketLimiter.RateLimitDecision second = limiter.Evaluate(endpoint);

        Assert.True(first.Allowed);
        Assert.False(second.Allowed);
        Assert.Equal(TokenBucketLimiter.RateLimitReason.SoftThrottle, second.Reason);
        Assert.True(second.RetryAfterMs >= 0);
    }

    [Fact]
    public void Evaluate_WhenInitialTokensIsZero_FirstRequestIsThrottled()
    {
        TokenBucketOptions options = CreateOptions();
        options.InitialTokens = 0;
        options.MaxSoftViolations = 5;

        using TokenBucketLimiter limiter = new(options);

        TokenBucketLimiter.RateLimitDecision decision = limiter.Evaluate(new TestEndpoint("127.0.0.20"));

        Assert.False(decision.Allowed);
        Assert.Equal(TokenBucketLimiter.RateLimitReason.SoftThrottle, decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenMaxTrackedEndpointsReached_ReturnsHardLockoutForNewEndpoint()
    {
        TokenBucketOptions options = CreateOptions();
        options.MaxTrackedEndpoints = 1;
        options.HardLockoutSeconds = 2;

        using TokenBucketLimiter limiter = new(options);

        TokenBucketLimiter.RateLimitDecision first = limiter.Evaluate(new TestEndpoint("10.0.0.1"));
        TokenBucketLimiter.RateLimitDecision second = limiter.Evaluate(new TestEndpoint("10.0.0.2"));

        Assert.True(first.Allowed);
        Assert.False(second.Allowed);
        Assert.Equal(TokenBucketLimiter.RateLimitReason.HardLockout, second.Reason);
        Assert.Equal(2000, second.RetryAfterMs);
    }

    [Fact]
    public void GetReportData_AfterMultipleEndpoints_ContainsExpectedTrackedCount()
    {
        using TokenBucketLimiter limiter = new(CreateOptions());

        _ = limiter.Evaluate(new TestEndpoint("192.168.0.11"));
        _ = limiter.Evaluate(new TestEndpoint("192.168.0.12"));

        IDictionary<string, object> report = limiter.GetReportData();

        Assert.Equal(2, report["TrackedEndpoints"]);
        Assert.True(report.ContainsKey("Endpoints"));
    }

    [Fact]
    public async Task DisposeAsync_AfterDispose_EvaluateReturnsHardLockoutDecision()
    {
        TokenBucketLimiter limiter = new(CreateOptions());
        await limiter.DisposeAsync();

        TokenBucketLimiter.RateLimitDecision decision = limiter.Evaluate(new TestEndpoint("172.16.10.20"));

        Assert.False(decision.Allowed);
        Assert.Equal(TokenBucketLimiter.RateLimitReason.HardLockout, decision.Reason);
    }

    [Fact]
    public void GenerateReport_AfterTraffic_ContainsExpectedHeader()
    {
        using TokenBucketLimiter limiter = new(CreateOptions());
        _ = limiter.Evaluate(new TestEndpoint("127.0.0.50"));

        string report = limiter.GenerateReport();

        Assert.Contains("TokenBucketLimiter Status", report, StringComparison.Ordinal);
        Assert.Contains("TrackedEndpoints", report, StringComparison.Ordinal);
    }

    private static TokenBucketOptions CreateOptions()
    {
        return new TokenBucketOptions
        {
            CapacityTokens = 2,
            RefillTokensPerSecond = 1,
            HardLockoutSeconds = 2,
            StaleEntrySeconds = 60,
            CleanupIntervalSeconds = 60,
            TokenScale = 1000,
            ShardCount = 2,
            SoftViolationWindowSeconds = 5,
            MaxSoftViolations = 3,
            CooldownResetSec = 10,
            MaxTrackedEndpoints = 100,
            InitialTokens = -1
        };
    }

    private sealed record TestEndpoint(string Address, int Port = 0, bool HasPort = false, bool IsIPv6 = false) : INetworkEndpoint
    {
    }
}
