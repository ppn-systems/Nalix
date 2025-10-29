# TokenBucketOptions — Configuration for Token-Bucket Rate Limiter (Backend/Networking)

The `TokenBucketOptions` class encapsulates all configuration settings for the high-performance token-bucket rate limiter used in .NET backend servers (API gateways, game networks, IoT, etc.).  
It covers burst, refill, soft/hard throttling, sharding, memory defense, and more.

---

## Properties

| Property                     | Type     | Default  | Description                                                                           |
|------------------------------|----------|----------|---------------------------------------------------------------------------------------|
| `CapacityTokens`             | int      | 12       | Max tokens held in bucket for any endpoint (determines burst size).                   |
| `RefillTokensPerSecond`      | double   | 6.0      | Token refill rate per second (sustained throughput).                                  |
| `HardLockoutSeconds`         | int      | 0        | Hard block time in seconds after max violation (0 disables).                          |
| `StaleEntrySeconds`          | int      | 300      | Seconds before a tracked endpoint is considered stale/expired.                        |
| `CleanupIntervalSeconds`     | int      | 120      | How often (in seconds) to clean up stale endpoints.                                   |
| `TokenScale`                 | int      | 1000     | Arithmetic granularity for tokens (e.g., 1000=millitoken; increases refill precision).|
| `ShardCount`                 | int      | 32       | Concurrency sharding count (must be a power of two).                                  |
| `SoftViolationWindowSeconds` | int      | 5        | Window over which soft violations are counted for escalation.                         |
| `MaxSoftViolations`          | int      | 3        | Number of soft violations (overzealous requests) allowed before escalation.           |
| `CooldownResetSec`           | int      | 10       | How long before violation/hardlock state resets (seconds).                            |
| `MaxTrackedEndpoints`        | int      | 10000    | Cap on number of simultaneously tracked endpoints (0 = no limit, not recommended).    |
| `InitialTokens`              | int      | -1       | How many tokens a new endpoint starts with (-1 = full, 0 = empty/cold-start).         |

---

## Validation

- **Validates all properties at runtime; call `.Validate()` before use.**
- `ShardCount` **must** be a power-of-two for correct/balanced parallelism.
- Extreme `TokenScale` or `CapacityTokens * TokenScale` are capped to prevent overflows.
- Zero refill disables token regeneration; possibly for unthrottled burst-only scenarios.

---

## Usage Example

```csharp
var options = new TokenBucketOptions
{
    CapacityTokens = 20,
    RefillTokensPerSecond = 5.5,
    ShardCount = 16, // Must be a power of 2
    MaxTrackedEndpoints = 20000,
    ... // Further customization
};
options.Validate();
// Pass to TokenBucketLimiter on construction
var limiter = new TokenBucketLimiter(options);
```

---

## Tuning Guidance

- **Set `CapacityTokens`** for command "burstiness" (short-burst: 5–10, media upload: 20+)
- **Lower `RefillTokensPerSecond`** for stricter throughput (API protection); higher for chat/real-time flows
- Use a **large enough `ShardCount`** (16–64) on multicore servers for best thread scaling
- Cap `MaxTrackedEndpoints` to fit your RAM budget

---

## Precision & Performance

- `TokenScale` controls arithmetic granularity; higher = more precision, slightly more memory/CPU.
- Granular sharding and optimized pool management enables hundreds of thousands of clients with minimal impact.

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2026 PPN Corporation.

---

## See Also

- [PoolingOptions](./PoolingOptions.md)
- [PolicyRateLimiter](../Throttling/PolicyRateLimiter.md)
- [TokenBucketLimiter](../Throttling/TokenBucketLimiter.md)
