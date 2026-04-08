# Token Bucket Limiter

`TokenBucketLimiter` is a per-endpoint rate limiter for ingress protection.

## Source mapping

- `src/Nalix.Network.Pipeline/Throttling/TokenBucketLimiter.cs`

## What it does

- keeps a token bucket per endpoint
- refills credit over time
- returns retry timing when traffic is denied
- escalates repeated abuse into hard lockouts
- supports configurable initial balance for new endpoints
- bounds memory with tracked-endpoint limits and cleanup

## Basic usage

```csharp
TokenBucketLimiter limiter = new(tokenBucketOptions);

var decision = limiter.Evaluate(endpoint);
if (!decision.Allowed)
{
    return;
}
```

## Decision model

The result tells you:

- whether the request is allowed
- how long to wait before retry
- how much credit remains
- whether the denial is soft throttle or hard lockout

## RateLimitDecision and RateLimitReason

`Evaluate(endpoint)` returns a `RateLimitDecision`.

Its main fields are:

- `Allowed`
- `RetryAfterMs`
- `Credit`
- `Reason`

`Reason` is a `RateLimitReason` value:

- `None` when the request is accepted
- `SoftThrottle` when the endpoint should back off but is not hard-blocked yet
- `HardLockout` when the endpoint is temporarily blocked due to repeated pressure or endpoint-limit enforcement

## Example

```csharp
var decision = limiter.Evaluate(endpoint);

if (!decision.Allowed)
{
    if (decision.Reason == RateLimitReason.HardLockout)
    {
        // deny and surface stronger retry/backoff messaging
    }

    int retryAfterMs = decision.RetryAfterMs;
}
```

`Credit` is especially useful for diagnostics and adaptive client behavior because it tells you how much whole-token budget remains after the check.

If `MaxTrackedEndpoints` is reached, new endpoint tracking attempts are denied with a hard-lockout-style decision until cleanup frees capacity.

## When to use it

Use this limiter when the key is the caller endpoint rather than the handler opcode.

## Diagnostics

`GenerateReport()` includes:

- tracked endpoint count
- hard-blocked count
- top endpoints by pressure
- current token settings

## Related APIs

- [Policy Rate Limiter](./policy-rate-limiter.md)
- [Connection Limiter](../../network/connection/connection-limiter.md)
