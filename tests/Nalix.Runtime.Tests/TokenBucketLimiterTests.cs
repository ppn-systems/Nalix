using System;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Abstractions.Networking;
using Nalix.Runtime.Options;
using Nalix.Runtime.Throttling;
using Xunit;

namespace Nalix.Runtime.Tests;

public sealed class TokenBucketLimiterTests
{
    [Fact]
    public void Evaluate_WithNullEndpoint_ShouldThrowArgumentNullException()
    {
        using var limiter = new TokenBucketLimiter(CreateOptions());
        Action act = () => limiter.Evaluate(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("key");
    }

    [Fact]
    public void Evaluate_WithEmptyAddress_ShouldThrowArgumentException()
    {
        using var limiter = new TokenBucketLimiter(CreateOptions());
        Action act = () => limiter.Evaluate(new TestEndpoint(""));
        act.Should().Throw<ArgumentException>().WithMessage("*address*");
    }

    [Fact]
    public void Evaluate_WithinBurstCapacity_ShouldAllowRequests()
    {
        var options = CreateOptions();
        options.CapacityTokens = 5;
        options.InitialTokens = 5;
        options.RefillTokensPerSecond = 0.001; // Minimal refill to pass validation while staying stable
        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("1.1.1.1");

        for (int i = 0; i < 5; i++)
        {
            var decision = limiter.Evaluate(endpoint);
            decision.Allowed.Should().BeTrue();
            decision.Credit.Should().Be((ushort)(4 - i));
        }

        limiter.Evaluate(endpoint).Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_AfterRefillTime_ShouldAllowNewRequests()
    {
        var options = CreateOptions();
        options.CapacityTokens = 1;
        options.InitialTokens = 1;
        options.RefillTokensPerSecond = 5; // Refill 1 token every 200ms
        options.TokenScale = 1000;
        
        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("2.2.2.2");

        // 1. Consume initial
        limiter.Evaluate(endpoint).Allowed.Should().BeTrue();
        
        // 2. Immediate next should be blocked (200ms window)
        limiter.Evaluate(endpoint).Allowed.Should().BeFalse();

        // 3. Wait for refill (200ms + buffer)
        await Task.Delay(400);

        limiter.Evaluate(endpoint).Allowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_RepeatedViolations_ShouldEscalateToHardLockout()
    {
        var options = CreateOptions();
        options.CapacityTokens = 1;
        options.InitialTokens = 1;
        options.MaxSoftViolations = 2;
        options.SoftViolationWindowSeconds = 10;
        options.HardLockoutSeconds = 60;
        
        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("3.3.3.3");

        // 1. Consume
        limiter.Evaluate(endpoint).Allowed.Should().BeTrue();

        // 2. First violation (soft)
        var soft1 = limiter.Evaluate(endpoint);
        soft1.Allowed.Should().BeFalse();
        soft1.Reason.Should().Be(TokenBucketLimiter.RateLimitReason.SoftThrottle);

        // 3. Second violation (triggers hard lockout)
        var hard = limiter.Evaluate(endpoint);
        hard.Allowed.Should().BeFalse();
        hard.Reason.Should().Be(TokenBucketLimiter.RateLimitReason.HardLockout);
        hard.RetryAfterMs.Should().BeInRange(59000, 61000);
    }

    [Fact]
    public void Evaluate_DynamicPolicy_ShouldUseProvidedPolicy()
    {
        using var limiter = new TokenBucketLimiter(CreateOptions());
        var endpoint = new TestEndpoint("4.4.4.4");

        // Create a strict policy (1 token capacity, 0 refill)
        var policy = new TokenBucketLimiter.RateLimitPolicy(
            rps: 0, // Disable refill 
            burst: 1.0, 
            tokenScale: 1000, 
            swFreq: System.Diagnostics.Stopwatch.Frequency, 
            hardLockoutSec: 10, 
            maxSoftViolations: 1);

        limiter.Evaluate(endpoint, policy).Allowed.Should().BeTrue();
        
        var second = limiter.Evaluate(endpoint, policy);
        second.Allowed.Should().BeFalse();
        second.Reason.Should().Be(TokenBucketLimiter.RateLimitReason.HardLockout);
    }

    [Fact]
    public void Evaluate_MaxTrackedEndpoints_ShouldRejectNewEndpoints()
    {
        var options = CreateOptions();
        options.MaxTrackedEndpoints = 2;
        
        using var limiter = new TokenBucketLimiter(options);
        
        limiter.Evaluate(new TestEndpoint("ip1")).Allowed.Should().BeTrue();
        limiter.Evaluate(new TestEndpoint("ip2")).Allowed.Should().BeTrue();
        
        var third = limiter.Evaluate(new TestEndpoint("ip3"));
        third.Allowed.Should().BeFalse();
        third.Reason.Should().Be(TokenBucketLimiter.RateLimitReason.HardLockout);
    }

    [Fact]
    public void GenerateReport_ShouldReturnFormattedString()
    {
        using var limiter = new TokenBucketLimiter(CreateOptions());
        limiter.Evaluate(new TestEndpoint("report-ip"));

        string report = limiter.GenerateReport();
        report.Should().Contain("TokenBucketLimiter Status");
        report.Should().Contain("report-ip");
    }

    [Fact]
    public void Dispose_AfterDisposed_ShouldReturnHardLockout()
    {
        var limiter = new TokenBucketLimiter(CreateOptions());
        limiter.Dispose();

        var decision = limiter.Evaluate(new TestEndpoint("any"));
        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(TokenBucketLimiter.RateLimitReason.HardLockout);
    }

    private static TokenBucketOptions CreateOptions()
    {
        return new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 10,
            HardLockoutSeconds = 5,
            StaleEntrySeconds = 60,
            CleanupIntervalSeconds = 60,
            TokenScale = 1000000,
            ShardCount = 8,
            SoftViolationWindowSeconds = 5,
            MaxSoftViolations = 5,
            MaxTrackedEndpoints = 1000,
            InitialTokens = -1 // Full bucket
        };
    }

    private sealed record TestEndpoint(string Address) : INetworkEndpoint
    {
        public int Port => 0;
        public bool HasPort => false;
        public bool IsIPv6 => false;
    }
}
