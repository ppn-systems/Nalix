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

## Core Properties

- capacity/rate: `CapacityTokens`, `RefillTokensPerSecond`
- penalties: `HardLockoutSeconds`, `SoftViolationWindowSeconds`, `MaxSoftViolations`, `CooldownResetSec`
- storage/cleanup: `StaleEntrySeconds`, `CleanupIntervalSeconds`, `MaxTrackedEndpoints`
- precision/sharding: `TokenScale`, `ShardCount`
- new-entry behavior: `InitialTokens`

## Validation Notes

- `ShardCount` must be power-of-two.
- `CapacityTokens * TokenScale` must fit `Int64`.

## Related APIs

- [Token Bucket Limiter](../../runtime/middleware/token-bucket-limiter.md)
- [Policy Rate Limiter](../../runtime/middleware/policy-rate-limiter.md)
