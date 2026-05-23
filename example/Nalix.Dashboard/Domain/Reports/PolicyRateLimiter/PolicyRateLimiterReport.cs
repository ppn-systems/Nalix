using Nalix.Dashboard.Domain.Reports.TokenBucketLimiter;

namespace Nalix.Dashboard.Domain.Reports.PolicyRateLimiter;

public sealed record PolicyRateLimiterReport(
    string? UtcNow,
    int CheckCounter,
    TokenBucketLimiterReport? SharedEngine);
