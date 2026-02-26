using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
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

    [Fact]
    public void NetworkEndpointInterfaceProperties_RemainAccessibleThroughInterface()
    {
        INetworkEndpoint endpoint = new TestEndpoint("10.0.0.5", 27015, HasPort: true, IsIPv6: false);

        Assert.Equal("10.0.0.5", endpoint.Address);
        Assert.Equal(27015, endpoint.Port);
        Assert.True(endpoint.HasPort);
        Assert.False(endpoint.IsIPv6);
    }

    [Fact]
    public async Task Evaluate_ConcurrentCalls_OnSameEndpoint_DoNotThrow()
    {
        TokenBucketOptions options = new()
        {
            CapacityTokens = 32,
            RefillTokensPerSecond = 16,
            CleanupIntervalSeconds = 60,
            StaleEntrySeconds = 60,
            ShardCount = 2,
            MaxTrackedEndpoints = 8
        };

        using TokenBucketLimiter limiter = new(options);
        TestEndpoint endpoint = new("172.16.0.10");

        Task<TokenBucketLimiter.RateLimitDecision>[] tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => limiter.Evaluate(endpoint)))
            .ToArray();

        TokenBucketLimiter.RateLimitDecision[] decisions = await Task.WhenAll(tasks);
        IDictionary<string, object> report = limiter.GetReportData();

        Assert.Equal(50, decisions.Length);
        Assert.All(decisions, static decision => Assert.InRange(decision.Credit, (ushort)0, ushort.MaxValue));
        Assert.Equal(1, report["TrackedEndpoints"]);
    }

    private sealed record TestEndpoint(string Address, int Port = 0, bool HasPort = false, bool IsIPv6 = false) : INetworkEndpoint
    {
    }
}
