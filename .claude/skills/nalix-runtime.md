# Nalix.Runtime

## Role

Packet processing and middleware infrastructure. Provides the middleware pipeline, packet dispatching channels (with weighted round-robin), handler registration, throttling, timekeeping, and packet context lifecycle.

**Dependencies:** `Nalix.Abstractions`, `Nalix.Framework`, `Nalix.Codec`

## Directory Structure

```
Nalix.Runtime/
├── Dispatching/         # Packet dispatch system (core message routing)
│   ├── IDispatchChannel.cs          # Channel abstraction
│   ├── IPacketDispatch.cs           # Dispatch contract
│   ├── IPacketMetadataProvider.cs   # Metadata provider contract
│   ├── InlinePacketDispatcher.cs    # Sync inline dispatcher
│   ├── PacketContext.cs             # Per-request context (pooled)
│   ├── PacketDispatchChannel.cs     # Channel-based async dispatcher with WRR
│   ├── PacketDispatcherBase.cs      # Abstract base for dispatchers
│   ├── PacketMetadataBuilder.cs     # Metadata registration builder
│   ├── PacketMetadataProviders.cs   # Provider implementations
│   ├── PacketSender.cs             # Outbound packet sending helper
│   └── Options/                     # DispatchOptions (weights, concurrency)
├── Extensions/          # Runtime extension methods
├── Handlers/            # Built-in system handlers
│   ├── HandshakeHandlers.cs         # Crypto handshake handling
│   ├── SessionHandlers.cs           # Session resume/management
│   └── SystemControlHandlers.cs     # Error, throttle, notice control packets
├── Internal/            # Internal helpers
├── Middleware/           # Middleware pipeline
│   ├── MiddlewarePipeline.cs        # Pipeline orchestration engine
│   └── Standard/                    # Built-in middleware implementations
├── Options/             # Runtime configuration options
├── Pooling/             # PacketContext pooling
├── Throttling/          # Rate limiting and backpressure
└── Timekeeping/         # Timing wheel and scheduling primitives
```

## Key Subsystems

### Packet Dispatch

The dispatch system routes deserialized packets to registered handlers:

1. `PacketDispatchChannel` — Async channel-based dispatcher using **Weighted Round-Robin (DRR)** to prevent priority starvation.
2. `InlinePacketDispatcher` — Synchronous inline variant for simple use cases.
3. `PacketContext` — Pooled per-request context carrying connection, packet, and metadata.
4. `PacketSender` — Helper for sending response packets back to connections.

### Middleware Pipeline

`MiddlewarePipeline` chains `IPacketMiddleware` implementations:
- Middleware is ordered by `[MiddlewareOrder]` attribute.
- Each middleware can short-circuit the pipeline.
- Stages: `MiddlewareStage.PreProcess`, `Process`, `PostProcess`.

### Built-in Handlers

| Handler | Purpose |
| :--- | :--- |
| `HandshakeHandlers` | X25519 key exchange, cipher suite negotiation |
| `SessionHandlers` | Session resume, snapshot validation |
| `SystemControlHandlers` | Error/fail handling, throttle feedback, maintenance notices |

### Throttling

Token-bucket and policy-based rate limiting with configurable burst and sustained rates.

### Timekeeping

O(1) `TimingWheel` for scheduling timeouts and recurring events without heap allocation.

## Performance Rules

- `PacketContext` is pooled — always return via `Dispose()`.
- Middleware pipeline MUST NOT allocate on the hot path.
- Dispatch channel uses `System.Threading.Channels` for backpressure.
- Handler resolution is O(1) via opcode-indexed lookup.

## Anti-Patterns

- Do NOT register middleware without `[MiddlewareOrder]` — ordering is mandatory.
- Do NOT hold references to `PacketContext` beyond the handler scope.
- Do NOT bypass `PacketSender` for outbound packets — it handles framing and transforms.
