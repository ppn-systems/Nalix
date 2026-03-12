# TokenBucketLimiter — High-Performance Per-Endpoint Token-Bucket Rate Limiter for .NET Servers

The `TokenBucketLimiter` implements a precise, thread-safe, per-endpoint token bucket rate-limiting system for .NET backends.
It is ideal for API gateways, multiplayer game servers, IoT brokers, or any high-concurrency service needing abuse-resilient ingress control.

---

## Key Features

- **Per-endpoint (per-IP)** rate limiting with global-tracking protection
- **Token bucket** algorithm, fixed-point counters for high-precision, even under bursty load
- **Soft throttling and hard lockouts:**  
  - Gentle slow-down on soft violation, escalate to temporary ban if abuse persists
- **Sliding window, retry-after, and remaining credit (tokens left) for client feedback**
- **Shard-based design** for parallelism and lock contention minimization
- **Stopwatch-based time calculations** for accurate refill and expiration (works cross-platform)
- **Resource-aware:**  
  - Limit max tracked endpoints (auto-evict LRU if overload)
  - Periodic cleanup of stale endpoints, with async-safe operation and cancellation
- **Diagnostics:**  
  - Live summary/report output with top endpoints, credit, block status, retry-after

---

## Configuration

Main options (via `TokenBucketOptions`, may use DI/ConfigManager):

- `CapacityTokens`: Max tokens per endpoint bucket (burst size)
- `RefillTokensPerSecond`: Token refill rate
- `TokenScale`: Resolution for fractional tokens (e.g. 1000=millitoken)
- `CleanupIntervalSeconds`: How often to clean up endpoints
- `StaleEntrySeconds`: How long an endpoint remains in-memory without use
- `MaxTrackedEndpoints`: Global memory bound (defense against "endpoint sprawl" attacks)
- `SoftViolationWindowSeconds`, `MaxSoftViolations`: Soft escalation settings (number of bad hits before hard ban)
- `HardLockoutSeconds`: Length of hard ban when endpoint is blocked

---

## Usage Example

```csharp
var limiter = new TokenBucketLimiter(myOptions);
var endpoint = new MyNetworkEndpoint(remoteIp);

var decision = limiter.Check(endpoint);
if (decision.Allowed) {
    // Proceed — consume 1 quota
} else {
    // Throttle/Drop, optionally inform client with decision.RetryAfterMs / Credit
}
```

---

## Decision API

`RateLimitDecision` result includes:

- `Allowed`: True (passed, token consumed); False (do not process)
- `RetryAfterMs`: Milliseconds until next token; use to inform client for robust backoff
- `Credit`: Remaining tokens (useful for clients to self-throttle)
- `Reason`: None, SoftThrottle, HardLockout

---

## Token Algorithm, Sharding & Parallelism

- Each endpoint has its own token bucket, recalculated via Stopwatch ticks (accurate under real-world clock skew and VM pauses)
- Multiple shards** prevent lock contention, allowing 10,000+ endpoints scale-out
- Cleanup is regular and enforced: old/idle endpoints and over-quota evict automatically

---

## Circuit Breaker & Abuse Defense

- **SoftThrottle:**  
  On occasional bursts, gently back off with calculated delay (no ban)
- **HardLockout:**  
  Persistent overuse → endpoint is hard-blocked for configurable seconds, with retry-after info returned
- **Eviction:**  
  If `MaxTrackedEndpoints` is exceeded, oldest endpoints are evicted and new ones can join.  
  This prevents RAM exhaustion under attack.

---

## Diagnostics & Reporting

`.GenerateReport()` gives full real-time state:

```log
[2026-03-12 15:12:00] TokenBucketLimiter Status:
CapacityTokens      : 10
RefillPerSecond     : 4.0
TokenScale          : 1000
Shards              : 8
HardLockoutSeconds  : 60
...
TrackedEndpoints    : 44
HardBlockedCount    : 1

Top Endpoints by Pressure:
-------------------------------------------------------------------------------
Endpoint(Key)    | Blocked | Credit | MicroBalance/Capacity | RetryAfter(ms)
-------------------------------------------------------------------------------
192.168.1.24     | yes     |      0 |      0/10000          |        58212
10.20.0.83       |  no     |     7  |   7000/10000          |           0
...
-------------------------------------------------------------------------------
```

---

## Best Practices

- **Don’t let endpoints fill up RAM:** Set `MaxTrackedEndpoints` to a reasonable value for your expected workload.
- **Tune token and refill rates** per command characteristics (lightweight=more tokens, heavy/expensive=low token).
- **Monitor your diagnostic report** for high "HardBlockedCount" or rapidly growing endpoint set — these can signal attacks or misbehaving clients.
- **Wire retry-after / credit details** into your protocol for smooth self-throttling on the client side.

---

## Thread Safety & Disposal

- All APIs are thread-safe, cleanup jobs are managed internally
- Call `.Dispose()` or `.DisposeAsync()` to stop background cleanup and forcibly release resources

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025-2026 PPN Corporation.

---

## See Also

- [MiddlewarePipeline](../Middleware/README.md)
- [PoolingOptions](../Configurations/PoolingOptions.md)
- [TokenBucketOptions](../Configurations/TokenBucketOptions.md)
- [PacketRateLimitAttribute](../../../src/Nalix.Common/Networking/Packets/Attributes/PacketRateLimitAttribute.cs)
