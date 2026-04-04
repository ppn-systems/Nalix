# Nalix.Network.Pipeline

`Nalix.Network.Pipeline` provides reusable middleware, throttling, and timekeeping components for the Nalix networking stack.

## Install

```bash
dotnet add package Nalix.Network.Pipeline
```

## What it includes

- Inbound packet middleware for permission, rate-limit, concurrency, and timeout checks
- Throttling primitives such as `ConcurrencyGate`, `PolicyRateLimiter`, and `TokenBucketLimiter`
- `TimeSynchronizer` and supporting options for distributed runtime coordination

## Typical use

Add this package when you want pipeline and throttling features without depending on the full transport runtime surface of `Nalix.Network`.

## Documentation

- Package docs: [Nalix.Network.Pipeline](https://ppn-systems.github.io/Nalix/packages/nalix-network-pipeline/)
- API docs: [Middleware API](https://ppn-systems.github.io/Nalix/api/middleware/pipeline/)
