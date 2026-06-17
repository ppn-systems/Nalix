# Dynamic Concurrency Adjustment

!!! danger "Deprecated"
    The `ConcurrencyGate`, `ConcurrencyMiddleware`, and `[PacketConcurrencyLimit]`
    attribute that this guide relied on have been removed from Nalix. Packet-level
    concurrency and rate limiting are now handled by `TokenBucketLimiter` and
    `PolicyRateLimiter` through the middleware pipeline using `[PacketRateLimit]`.

## Current Approach

To limit concurrent or per-endpoint traffic in the current Nalix runtime:

1. **Per-handler rate limiting** — Use `[PacketRateLimit(requestsPerSecond, burst)]` on
   handler methods. `RateLimitMiddleware` enforces this automatically.
2. **Global endpoint throttling** — Configure `TokenBucketOptions` for connection-level
   token bucket limits.
3. **Custom middleware** — Implement `IPacketMiddleware<TPacket>` for application-specific
   throttling logic (e.g., per-tenant budgets).

## Related Information

- [Token Bucket Limiter](../../api/runtime/middleware/token-bucket-limiter.md)
- [Policy Rate Limiter](../../api/runtime/middleware/policy-rate-limiter.md)
- [Custom Middleware Guide](../extensibility/custom-middleware.md)
