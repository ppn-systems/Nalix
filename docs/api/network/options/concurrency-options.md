# Concurrency Options

`ConcurrencyOptions` configures the global concurrency gate and circuit-breaker behavior in `Nalix.Network.Pipeline`.

## Source Mapping

- `src/Nalix.Network.Pipeline/Options/ConcurrencyOptions.cs`

## Properties and Validation

| Property | Default | Validation | Runtime effect |
|---|---:|---|---|
| `CircuitBreakerThreshold` | `0.95` | `0.1..1.0` | Rejection-rate threshold that trips the circuit breaker. |
| `CircuitBreakerMinSamples` | `1000` | `10..1000000` | Minimum samples required before the circuit breaker can trip. |
| `CircuitBreakerResetAfterSeconds` | `60` | `1..3600` | Seconds to keep the circuit breaker open before attempting reset. |
| `MinIdleAgeMinutes` | `10` | `1..1440` | Minimum idle age before an opcode entry is considered for cleanup. |
| `CleanupIntervalMinutes` | `1` | `1..60` | Interval between idle entry cleanup cycles. |
| `WaitTimeoutSeconds` | `20` | `1..300` | Default timeout for `EnterAsync` operations when queuing is enabled. |

## Validation Notes

`Validate()` uses data-annotation validation and does not add cross-field rules beyond the ranges shown above.

## Related APIs

- [Network Options](./options.md)
- [Token Bucket Options](./token-bucket-options.md)
