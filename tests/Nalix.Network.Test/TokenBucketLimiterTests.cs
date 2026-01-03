using Nalix.Common.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Throttling;
using System;
using System.Collections.Generic;
using Xunit;

namespace Nalix.Network.Tests;

/// <summary>
/// Tests for TokenBucketLimiter performance and security improvements.
/// </summary>
public sealed class TokenBucketLimiterTests
{
    /// <summary>
    /// Mock endpoint for testing.
    /// </summary>
    private sealed class TestEndpoint : INetworkEndpoint
    {
        public string Address { get; }

        public TestEndpoint(string address)
        {
            Address = address;
        }

        public override int GetHashCode() => Address.GetHashCode();

        public override bool Equals(object obj) => obj is TestEndpoint other && Address == other.Address;
    }

    [Fact]
    public void ValidateOptions_ShouldRejectInvalidCapacity()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 0 // Invalid
        };

        // Act & Assert
        var ex = Assert.Throws<Nalix.Common.Exceptions.InternalErrorException>(() => 
            new TokenBucketLimiter(options));
        Assert.Contains("CapacityTokens", ex.Message);
        Assert.Contains("must be > 0", ex.Message);
    }

    [Fact]
    public void ValidateOptions_ShouldRejectInvalidRefillRate()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 0 // Invalid
        };

        // Act & Assert
        var ex = Assert.Throws<Nalix.Common.Exceptions.InternalErrorException>(() => 
            new TokenBucketLimiter(options));
        Assert.Contains("RefillTokensPerSecond", ex.Message);
        Assert.Contains("must be > 0", ex.Message);
    }

    [Fact]
    public void ValidateOptions_ShouldRejectInvalidShardCount()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5,
            ShardCount = 7 // Not a power of 2
        };

        // Act & Assert
        var ex = Assert.Throws<Nalix.Common.Exceptions.InternalErrorException>(() => 
            new TokenBucketLimiter(options));
        Assert.Contains("ShardCount", ex.Message);
        Assert.Contains("power of two", ex.Message);
    }

    [Fact]
    public void ValidateOptions_ShouldRejectNegativeMaxTrackedEndpoints()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5,
            ShardCount = 32,
            MaxTrackedEndpoints = -1 // Invalid
        };

        // Act & Assert
        var ex = Assert.Throws<Nalix.Common.Exceptions.InternalErrorException>(() => 
            new TokenBucketLimiter(options));
        Assert.Contains("MaxTrackedEndpoints", ex.Message);
        Assert.Contains("must be >= 0", ex.Message);
    }

    [Fact]
    public void ValidateOptions_ShouldAcceptValidConfiguration()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5,
            ShardCount = 32,
            MaxTrackedEndpoints = 1000,
            TokenScale = 1000,
            StaleEntrySeconds = 300,
            CleanupIntervalSeconds = 60
        };

        // Act & Assert - should not throw
        using var limiter = new TokenBucketLimiter(options);
        Assert.NotNull(limiter);
    }

    [Fact]
    public void Check_ShouldRejectNullEndpoint()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5
        };
        using var limiter = new TokenBucketLimiter(options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            limiter.Check(null!));
    }

    [Fact]
    public void Check_ShouldRejectEmptyAddress()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5
        };
        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            limiter.Check(endpoint));
    }

    [Fact]
    public void Check_ShouldThrottleInitialRequestWhenNoTokens()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5
        };
        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("192.168.1.1");

        // Act
        var decision = limiter.Check(endpoint);

        // Assert - new endpoints start with 0 balance, so first request should be throttled
        // This is the correct behavior as it prevents burst attacks from new endpoints
        Assert.False(decision.Allowed);
        Assert.Equal(TokenBucketLimiter.RateLimitReason.SoftThrottle, decision.Reason);
        Assert.True(decision.RetryAfterMs > 0);
    }

    [Fact]
    public void GenerateReport_ShouldIncludeMaxTrackedEndpoints()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5,
            MaxTrackedEndpoints = 5000
        };
        using var limiter = new TokenBucketLimiter(options);

        // Act
        var report = limiter.GenerateReport();

        // Assert
        Assert.NotNull(report);
        Assert.Contains("MaxTrackedEndpoints", report);
        Assert.Contains("5000", report);
    }

    [Fact]
    public void GenerateReport_ShouldHandleMultipleEndpoints()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5
        };
        using var limiter = new TokenBucketLimiter(options);

        // Create some endpoints
        var endpoints = new List<TestEndpoint>();
        for (int i = 0; i < 5; i++)
        {
            endpoints.Add(new TestEndpoint($"192.168.1.{i}"));
        }

        // Make requests to register endpoints
        foreach (var endpoint in endpoints)
        {
            limiter.Check(endpoint);
        }

        // Act
        var report = limiter.GenerateReport();

        // Assert
        Assert.NotNull(report);
        Assert.Contains("TrackedEndpoints", report);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5
        };
        var limiter = new TokenBucketLimiter(options);

        // Act & Assert - should not throw
        limiter.Dispose();
        
        // Verify disposed state by expecting ObjectDisposedException on Check
        var endpoint = new TestEndpoint("192.168.1.1");
        Assert.Throws<ObjectDisposedException>(() => limiter.Check(endpoint));
    }

    [Fact]
    public void DefaultOptions_ShouldHaveReasonableDefaults()
    {
        // Act
        var options = new TokenBucketOptions();

        // Assert - verify defaults are sensible
        Assert.True(options.CapacityTokens > 0);
        Assert.True(options.RefillTokensPerSecond > 0);
        Assert.True(options.TokenScale > 0);
        Assert.True(options.ShardCount > 0);
        Assert.True((options.ShardCount & (options.ShardCount - 1)) == 0); // Power of 2
        Assert.True(options.MaxTrackedEndpoints > 0);
        Assert.Equal(10_000, options.MaxTrackedEndpoints);
    }
}
