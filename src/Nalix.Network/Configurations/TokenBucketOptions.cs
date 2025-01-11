using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Options for token-bucket limiter. You can map your legacy RateLimitOptions into this.
/// </summary>
public sealed class TokenBucketOptions : ConfigurationLoader
{
    /// <summary>Maximum tokens (bucket capacity). Typical: 10..100.</summary>
    public System.Int32 CapacityTokens { get; set; } = 20;

    /// <summary>Refill rate in tokens per second. Typical: Capacity / windowSeconds.</summary>
    public System.Double RefillTokensPerSecond { get; set; } = 10.0;

    /// <summary>
    /// Optional hard lockout seconds after a throttle decision. If 0, no hard lockout (soft backoff only).
    /// </summary>
    public System.Int32 HardLockoutSeconds { get; set; } = 0;

    /// <summary>
    /// Seconds after which an idle endpoint entry is considered stale and removed by cleanup.
    /// </summary>
    public System.Int32 StaleEntrySeconds { get; set; } = 300;

    /// <summary>
    /// Cleanup interval in seconds.
    /// </summary>
    public System.Int32 CleanupIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Fixed-point resolution for token arithmetic (micro-tokens per token). 1_000_000 by default.
    /// </summary>
    public System.Int32 TokenScale { get; set; } = 1_000_000;

    /// <summary>
    /// Number of shards for endpoint partitioning. Power-of-two recommended (e.g., 64).
    /// </summary>
    public System.Int32 ShardCount { get; set; } = 64;
}