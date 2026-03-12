# PolicyRateLimiter — Tiered RPS/Burst Policy Rate Limiter for .NET Backend

The `PolicyRateLimiter` manages multiple rate-limiting policies (per OpCode, packet type, command, or tenant) using **shared, quantized policies** and a pool of underlying token-bucket limiters.  
It is a fast, memory-safe, production-grade solution for high-throughput command dispatch, game servers, API gateways, or microservice APIs needing many rate shape variants.

---

## Key Features

- **Policy-based rate limiting:**  
  Rate limits are assigned via `[PacketRateLimit(rps, burst)]` per handler or via context.
- **Tier quantization:**  
  Only a fixed set of rates (RPS) and bursts are allowed; similar policies share the same limiter for efficiency.
  - Reduces memory and resource overhead versus creating thousands of unique limiters.
- **Supports hundreds of concurrent mixed policies** (up to configurable maximum, default 64).
- **Token bucket backend:**  
  Each policy uses a dedicated `TokenBucketLimiter` (see that doc for tuning and credit/retry-after features).
- **Automatic eviction of stale/unused policies:**  
  Policies with no recent access are periodically removed and cleaned up.
- **Thread-safe** and optimized for parallel multi-core environments.
- **Diagnostics/reporting:**  
  Exposes live report of all active policies, usage timestamps, and tiered structure.
- **Graceful shutdown:**  
  Policies and their per-policy limiters are robustly disposed and drained.

---

## Usage in a Handler

```csharp
[PacketRateLimit(8, burst: 2.5)]
public async Task HandleChat(ChatPacket p, IConnection c) { ... }
```

- Handler will have its own RPS (requests-per-second) and burst limit, mapped to the *nearest tier* of available policies.

### Rate-Limiting Check

In your dispatch pipeline or packet handler:

```csharp
var decision = policyRateLimiter.Check(opCode, packetContext);
if (!decision.Allowed) {
    // Reject/throttle/return Retry-After/etc.
}
```

- `decision.RetryAfterMs`: how long until next allowed
- `decision.Credit`: current burst tokens left (for feedback/self-throttling)

---

## Tiers & Policy Sharing

- RPS Tiers: `[1, 2, 4, 8, 16, 32, 64, 128]`
- Burst Tiers: `[0.1, 0.2, 0.5, 1, 2, 4, 8, 16, 32, 64]`
- Any attribute value is rounded UP to the next available tier (e.g. 6 RPS → 8, burst 3 → 4).

---

## Resource Management

- **MaxPolicies**: default 64 (configurable) — if at capacity, reuses closest matching tier; prevents memory exhaustion.
- **Automatic eviction:**  
  Unused (in last 30 minutes) policies removed with periodic sweeps.
- **Per-policy limiter sharing:**  
  Handlers/commands with same (tiered) RPS/burst always use same limiter.

---

## Diagnostics & Report

```csharp
string summary = policyRateLimiter.GenerateReport();
Console.WriteLine(summary);
```

Example Output:

```log
[2026-03-12 15:20:00] PolicyRateLimiter Status:
Active Policies    : 6/64
Check Counter      : 2,405

Active Policies:
------------------------------------------------------------
RPS  | Burst | Last Used (UTC)
------------------------------------------------------------
  16 |     4 | 2026-03-12 15:18:05
   8 |     2 | 2026-03-12 15:17:59
 ...
------------------------------------------------------------
```

---

## Best Practices

- **Annotate each handler** with realistic `[PacketRateLimit]` RPS/burst based on command workload.
- **Tune MaxPolicies** for your expected endpoint/command set.  
- **Don't abuse with unique RPS/burst values:** use "buckets" (common tiers) to maximize cache share.
- Regularly monitor reporting output in production for unexpected growth or tier skew.

---

## Thread Safety

- Fully thread-safe: any thread may invoke `Check`, `Dispose`, or reporting concurrently.

---

## Disposal

On application shutdown (or reload):

```csharp
policyRateLimiter.Dispose();
```

This releases all policy resources and underlying token-bucket limiters.

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025-2026 PPN Corporation.

---

## See Also

- [TokenBucketLimiter](./TokenBucketLimiter.md)
- [PoolingOptions](../Configurations/PoolingOptions.md)
- [PacketRateLimitAttribute](../../../src/Nalix.Common/Networking/Packets/Attributes/PacketRateLimitAttribute.cs)
