# TokenBucketOptions — Configure token-bucket rate limiting

`TokenBucketOptions` tunes the `TokenBucketLimiter` behavior (burst capacity, refill rate, violation policy, sharding, cleanup, and scalability).

---

## Key properties

| Property | Description | Default |
|----------|-------------|---------|
| `CapacityTokens` | Bucket capacity (tokens) — how many requests can burst at once (must be ≥ 1). | `12` |
| `RefillTokensPerSecond` | Sustained throughput rate (tokens/sec). | `6.0` |
| `TokenScale` | Fixed-point precision (tokens translated into micro-tokens for accurate arithmetic). | `1_000` |
| `ShardCount` | Number of shards partitioning the endpoint map (should be a power of two, e.g., 32). | `32` |
| `HardLockoutSeconds` | Hard lockout duration (seconds) after repeated violations (`0` disables hard lockouts). | `0` |
| `SoftViolationWindowSeconds` / `MaxSoftViolations` | Number of soft violations allowed within the window before harsher penalties kick in. | `5s` / `3` |
| `CooldownResetSec` | Seconds to reset violation counters after applying penalties. | `10` |
| `StaleEntrySeconds` | How long an idle endpoint stays in memory before eligible for cleanup. | `300` |
| `CleanupIntervalSeconds` | Frequency (seconds) of eviction scans for stale endpoints. | `120` |
| `MaxTrackedEndpoints` | Global cap on tracked endpoints to keep memory bounded (`0` = unlimited). | `10_000` |
| `InitialTokens` | Initial token count for new endpoints (`-1` = full capacity, `0` = cold start). | `-1` |

---

## Usage guidance

- Load via `ConfigurationManager.Instance.Get<TokenBucketOptions>()` or supply to the limiter’s constructor.
- Call `Validate()` to enforce power-of-two `ShardCount`, positive capacity, and overflow-safe arithmetic.
- Use low `CapacityTokens` / `RefillTokensPerSecond` for expensive commands; higher values for lightweight APIs that can accept bursts.
- Set `MaxTrackedEndpoints` to your expected concurrency; large values consume more memory, tiny values may evict legitimate users.
- Pair `TokenBucketOptions` with `[PacketRateLimit]` attributes so multiple handlers sharing the same tier reuse a single limiter.

---

## See also

- [TokenBucketLimiter](../Throttling/TokenBucketLimiter.md)
- [PolicyRateLimiter](../Throttling/PolicyRateLimiter.md)
