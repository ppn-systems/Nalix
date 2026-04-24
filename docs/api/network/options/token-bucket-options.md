# Token Bucket Options

`TokenBucketOptions` configures token-bucket limiter behavior in `Nalix.Network.Pipeline`.

## Audit Summary

- The earlier page covered key properties but did not fully align with the consistency template.
- Validation semantics needed explicit tie-in with rationale.

## Missing Content Identified

- Explicit rationale for power-of-two sharding and precision bounds.
- Uniform audit framing with other option docs.

## Improvement Rationale

Consistent presentation makes operational tuning safer and easier to compare across middleware controls.

## Source Mapping

- `src/Nalix.Network.Pipeline/Options/TokenBucketOptions.cs`

## Properties and Validation

| Property | Default | Validation | Runtime effect |
|---|---:|---|---|
| `CapacityTokens` | `12` | `1..int.MaxValue` | Maximum burst size in tokens. |
| `RefillTokensPerSecond` | `6.0` | `0.001..double.MaxValue` | Sustained token refill rate per second. |
| `HardLockoutSeconds` | `0` | `0..int.MaxValue` | Hard lockout duration after throttling; `0` disables hard lockout. |
| `StaleEntrySeconds` | `300` | `1..int.MaxValue` | Idle endpoint age before an entry is eligible for cleanup. |
| `CleanupIntervalSeconds` | `120` | `1..int.MaxValue` | Interval for purging stale endpoint entries. |
| `TokenScale` | `1000` | `1..1000000` | Fixed-point precision for token arithmetic. |
| `ShardCount` | `32` | `1..int.MaxValue`; must be power of two | Endpoint partition shard count. |
| `SoftViolationWindowSeconds` | `5` | `1..int.MaxValue` | Time window for counting soft rate-limit violations. |
| `MaxSoftViolations` | `3` | `1..int.MaxValue` | Soft violations allowed before stricter penalties apply. |
| `CooldownResetSec` | `10` | `1..int.MaxValue` | Seconds before violation count or lockout state resets after a penalty. |
| `MaxTrackedEndpoints` | `10000` | `0..int.MaxValue` | Maximum tracked endpoints; `0` means unlimited and is not recommended. |
| `InitialTokens` | `-1` | No data-annotation range | Initial tokens for new endpoints; `-1` means full capacity, `0` means cold start. |
| `MaxEvictionCapacity` | `4096` | `64..65536` | Maximum items processed per cleanup cycle. |
| `MinReportCapacity` | `256` | `64..8192` | Initial capacity for diagnostic report generation. |

## Validation Notes

- Data annotations validate each individual range shown above.
- `ShardCount` must be a power of two.
- `CapacityTokens * TokenScale` must fit in `Int64`.

## Related APIs

- [Token Bucket Limiter](../../runtime/middleware/token-bucket-limiter.md)
- [Policy Rate Limiter](../../runtime/middleware/policy-rate-limiter.md)
