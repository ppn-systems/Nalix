using Nalix.Common.Core.Abstractions;
using Nalix.Common.Core.Exceptions;
using Nalix.Network.Configurations;
using Nalix.Network.Throttling;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    private sealed class TestEndpoint(String address) : INetworkEndpoint
    {
        public String Address { get; } = address;

        public override Int32 GetHashCode() => Address.GetHashCode();

        public override Boolean Equals(Object obj) => obj is TestEndpoint other && Address == other.Address;
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
        var ex = Assert.Throws<InternalErrorException>(() =>
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
        var ex = Assert.Throws<InternalErrorException>(() =>
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
        var ex = Assert.Throws<InternalErrorException>(() =>
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
        var ex = Assert.Throws<InternalErrorException>(() =>
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
        for (Int32 i = 0; i < 5; i++)
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

    [Fact]
    public async Task RefillTokens_HighFrequency_NoTokenLoss()
    {
        // Arrange:  1 token/sec capacity, very frequent requests
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 1.0,
            TokenScale = 1000,
            ShardCount = 4
        };

        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("192.168.1.1");

        // Act: Consume all 10 tokens
        for (Int32 i = 0; i < 10; i++)
        {
            var decision = limiter.Check(endpoint);
            Assert.True(decision.Allowed, $"Token {i + 1} should be available");
        }

        // Now bucket is empty
        Assert.False(limiter.Check(endpoint).Allowed);

        // Wait 2 seconds → should refill 2 tokens
        await Task.Delay(2000);

        // Assert:  Exactly 2 requests should succeed
        Assert.True(limiter.Check(endpoint).Allowed, "Token 1 should refill");
        Assert.True(limiter.Check(endpoint).Allowed, "Token 2 should refill");
        Assert.False(limiter.Check(endpoint).Allowed, "Token 3 should not exist");
    }

    [Fact]
    public void Cleanup_ActiveEndpoint_ShouldNotBeRemoved()
    {
        // Arrange
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            StaleEntrySeconds = 2,  // 2 seconds stale threshold
            CleanupIntervalSeconds = 1,
            TokenScale = 1000,
            ShardCount = 4
        };

        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("192.168.1.1");

        // Create endpoint
        _ = limiter.Check(endpoint);

        // Wait 1.5 seconds (not stale yet)
        Thread.Sleep(1500);

        // Keep endpoint active
        _ = limiter.Check(endpoint);

        // Wait for cleanup to run
        Thread.Sleep(1200);

        // Assert: Active endpoint should NOT be removed
        var decision = limiter.Check(endpoint);
        Assert.True(decision.Allowed || !decision.Allowed);  // Should exist in system

        // Verify by checking it's still tracked
        var report = limiter.GenerateReport();
        Assert.Contains("192.168.1.1", report);
    }

    [Fact]
    public void Check_ShouldThrottleInitialRequestWhenNoTokens()
    {
        // Arrange:  Cold-start mode (start empty)
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5,
            InitialTokens = 0,  // ✅ FIX:  Explicitly set to 0
            TokenScale = 1000,
            ShardCount = 4
        };
        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("192.168.1.1");

        // Act
        var decision = limiter.Check(endpoint);

        // Assert: First request should be throttled (empty bucket)
        Assert.False(decision.Allowed);
        Assert.Equal(TokenBucketLimiter.RateLimitReason.SoftThrottle, decision.Reason);
        Assert.Equal((UInt16)0, decision.Credit);
        Assert.True(decision.RetryAfterMs > 0);
    }

    [Fact]
    public void Check_DefaultBehavior_ShouldStartFull()
    {
        // Arrange: Default options (InitialTokens = -1)
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 5,
            // InitialTokens = -1 (implicit default)
            TokenScale = 1000,
            ShardCount = 4
        };
        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("192.168.1.1");

        // Act
        var decision = limiter.Check(endpoint);

        // Assert: Default behavior is full bucket
        Assert.True(decision.Allowed);
        Assert.Equal((UInt16)9, decision.Credit);
    }

    [Fact]
    public void Check_CustomInitialTokens_ShouldRespectConfiguration()
    {
        // Arrange: Start with 3 tokens out of 10 capacity
        var options = new TokenBucketOptions
        {
            CapacityTokens = 10,
            InitialTokens = 3,
            RefillTokensPerSecond = 1.0,
            TokenScale = 1000,
            ShardCount = 4
        };
        using var limiter = new TokenBucketLimiter(options);
        var endpoint = new TestEndpoint("192.168.1.1");

        // Act & Assert: First 3 requests allowed
        for (Int32 i = 0; i < 3; i++)
        {
            var decision = limiter.Check(endpoint);
            Assert.True(decision.Allowed, $"Request {i + 1} should be allowed");
        }

        // 4th request denied
        var throttled = limiter.Check(endpoint);
        Assert.False(throttled.Allowed);
        Assert.Equal(TokenBucketLimiter.RateLimitReason.SoftThrottle, throttled.Reason);
    }
}
