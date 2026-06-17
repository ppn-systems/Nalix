# Concurrency Gate

!!! danger "Removed"
    `ConcurrencyGate`, `ConcurrencyMiddleware`, and `[PacketConcurrencyLimit]` have
    been removed from Nalix. Packet-level concurrency and rate limiting are now handled
    by `TokenBucketLimiter` and `PolicyRateLimiter` through the middleware pipeline.

## Current Alternatives

| Previous feature | Current replacement |
| --- | --- |
| `[PacketConcurrencyLimit]` attribute | `[PacketRateLimit]` attribute with `RateLimitMiddleware` |
| `ConcurrencyGate` per-opcode limiting | `TokenBucketLimiter` endpoint throttling + `PolicyRateLimiter` per-opcode |
| `ConcurrencyOptions` circuit breaker | `ConcurrencyOptions` class remains available for custom implementations |

## Related APIs

- [Middleware Pipeline](./pipeline.md)
- [Policy Rate Limiter](./policy-rate-limiter.md)
- [Token Bucket Limiter](./token-bucket-limiter.md)
- [Rate Limit Middleware](./pipeline.md#ratelimitmiddleware)
