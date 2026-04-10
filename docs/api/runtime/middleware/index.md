# Nalix.Runtime.Middleware

Middleware allows you to intercept and process packets or raw buffers before they reach the handler.

## Core Pipelines

### [Packet Pipeline](./pipeline.md)
The main pipeline for typed packet processing. This is where authentication, rate limiting, and auditing usually happen.

### [Network Buffer Pipeline](./network-buffer-pipeline.md)
A lower-level pipeline that operates on raw `IBufferLease` objects before deserialization.

## Built-in Middlewares

- [Concurrency Gate](./concurrency-gate.md): Limits the number of in-flight handlers.
- [Permission Middleware](./permission-middleware.md): Enforces connection-level permission checks.
- [Policy Rate Limiter](./policy-rate-limiter.md): Implements complex rate-limiting policies.
- [Timeout Middleware](./timeout-middleware.md): Enforces processing timeouts for packet handlers.
- [Policy Rate Limiter](./policy-rate-limiter.md): Implements complex rate-limiting policies.
