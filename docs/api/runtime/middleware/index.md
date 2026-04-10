# Nalix.Runtime.Middleware

Middleware allows you to intercept and process packets or raw buffers before they reach the handler.

## Core Pipelines

### [Packet Pipeline](./pipeline.md)
The main pipeline for typed packet processing. This is where authentication, rate limiting, and auditing usually happen.

### [Network Buffer Pipeline](./network-buffer-pipeline.md)
A lower-level pipeline that operates on raw `IBufferLease` objects before deserialization.

## Built-in Middlewares

- [Concurrency Gate](./concurrency-gate.md): Limits the number of in-flight handlers.
- [Policy Rate Limiter](./policy-rate-limiter.md): Implements complex rate-limiting policies.
- [Token Bucket Limiter](./token-bucket-limiter.md): A high-performance, predictable rate limiter.
