namespace Nalix.Dashboard.Domain.Reports.TokenBucketLimiter;

public sealed record TokenBucketLimiterReport(
    string? UtcNow,
    double CapacityTokens,
    double RefillPerSecond,
    double TokenScale,
    int Shards,
    int HardLockoutSeconds,
    int StaleEntrySeconds,
    int CleanupIntervalSecs,
    int MaxTrackedEndpoints,
    int TrackedEndpoints,
    int HardBlockedCount,
    IReadOnlyList<TokenBucketEndpoint>? Endpoints);

public sealed record TokenBucketEndpoint(
    string? Endpoint,
    bool Blocked,
    int Credit,
    long MicroBalance,
    int RetryAfterMs);
