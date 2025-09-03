using Nalix.Common.Exceptions;
using Nalix.Network.Configurations;
using Nalix.Network.Throttling;
using System.Threading.Tasks;
using Xunit;

namespace Nalix.Network.Tests.Throttling;

/// <summary>
/// Unit tests for <see cref="RequestLimiter"/> config & input validation.
/// </summary>
public sealed class RequestLimiterTests
{
    [Fact]
    public void Ctor_ShouldThrow_ForInvalidConfiguration()
    {
        var invalid = new TokenBucketOptions
        {
            CapacityTokens = 0,
            RefillTokensPerSecond = 1000,
            HardLockoutSeconds = 1
        };

        _ = Assert.Throws<InternalErrorException>(() => new RequestLimiter(invalid));
    }

    [Fact]
    public async Task CheckLimitAsync_ShouldThrow_ForNullOrWhitespaceEndpoint()
    {
        var valid = new TokenBucketOptions
        {
            CapacityTokens = 10,
            RefillTokensPerSecond = 1000,
            HardLockoutSeconds = 1
        };

        var limiter = new RequestLimiter(valid);
        _ = await Assert.ThrowsAsync<InternalErrorException>(() => limiter.CheckAsync("   ").AsTask());
    }
}
