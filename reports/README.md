# Benchmark Reports

This directory contains performance, stress, scalability, and telemetry reports for the Nalix networking framework.

The reports focus not only on raw throughput (RPS), but also on deeper system behavior including:

- Latency distribution (P50 / P95 / P99 / P99.9)
- CPU and memory utilization
- Buffer pool efficiency
- Object pool hit rates
- Garbage collection behavior
- Queue saturation and backpressure effects
- Middleware execution characteristics
- Load shedding and packet rejection behavior

## Report Notes

Some benchmark values may represent internal pipeline metrics rather than complete end-to-end network timing.

Examples:

- Client-side socket injection latency
- Queue waiting latency
- Middleware execution latency
- Application processing latency

These values should not always be interpreted as full network round-trip latency.

## Architecture Reference

The reports frequently reference internal Nalix terminology:

- Layer-4 Load Shedding
- Layer-7 Middleware
- BufferLease
- PacketContext
- Worker Sharding
- Dispatch Channels
- Backpressure

For a detailed explanation of how these components interact:

- [Nalix Architecture: Understanding System Layers & DDoS Protection](./LAYER_ARCHITECTURE.md)