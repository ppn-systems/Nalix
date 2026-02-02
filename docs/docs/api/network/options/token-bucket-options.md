# TokenBucketOptions

`TokenBucketOptions` configures the per-endpoint token-bucket limiter.

## Source mapping

- `src/Nalix.Network/Configurations/TokenBucketOptions.cs`

## What it controls

- bucket capacity
- refill rate
- hard lockout duration
- cleanup cadence
- stale entry timeout
- token precision
- shard count
- soft-violation escalation behavior
- maximum tracked endpoints
- initial token balance for newly seen endpoints

## Basic usage

```csharp
TokenBucketOptions options = ConfigurationManager.Instance.Get<TokenBucketOptions>();
options.Validate();
```

## Important note

`ShardCount` must be a power of two in the current implementation.

`InitialTokens` controls how new endpoints start:

- `-1` starts with a full bucket
- `0` starts empty for cold-start behavior
- positive values start with that many tokens, clamped to capacity

## Related APIs

- [Token Bucket Limiter](../../middleware/token-bucket-limiter.md)
- [Policy Rate Limiter](../../middleware/policy-rate-limiter.md)
