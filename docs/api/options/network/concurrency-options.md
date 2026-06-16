# Concurrency Options

`ConcurrencyOptions` provides configuration for concurrency-related circuit-breaker
thresholds in `Nalix.Runtime`.

## Source Mapping

- `src/Nalix.Runtime/Options/ConcurrencyOptions.cs`

## Defaults and Validation

| Property | Default | Valid range | Description |
| --- | ---: | --- | --- |
| `CircuitBreakerThreshold` | `0.95` | `0.1..1.0` | Rejection rate threshold to trip the circuit breaker. |
| `CircuitBreakerMinSamples` | `1000` | `10..1000000` | Minimum attempts required before the circuit breaker can trip. |
| `CircuitBreakerResetAfterSeconds` | `60` | `1..3600` | Duration to keep the circuit breaker open before resetting. |
| `MinIdleAgeMinutes` | `10` | `1..1440` | Minimum age before an idle entry is eligible for cleanup. |
| `CleanupIntervalMinutes` | `1` | `1..60` | Recurring cleanup cadence. |
| `WaitTimeoutSeconds` | `20` | `1..300` | Default timeout for queued entry operations. |

`Validate()` runs DataAnnotation validation only. There are no additional cross-field
rules in the current source.

!!! note "Current status"
    The `ConcurrencyGate` and `ConcurrencyMiddleware` that previously consumed these
    options have been removed. Packet-level concurrency throttling is now handled by
    `TokenBucketLimiter` and `PolicyRateLimiter` through the middleware pipeline. The
    `ConcurrencyOptions` class remains available for custom implementations or future use.

## Related APIs

- [Network Options](./options.md)
- [Token Bucket Options](./token-bucket-options.md)
- [Runtime Middleware Pipeline](../../runtime/middleware/pipeline.md)
