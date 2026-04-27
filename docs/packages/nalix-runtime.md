# Nalix.Runtime

`Nalix.Runtime` is the high-performance orchestration layer of the Nalix framework, specifically designed to power **Server-Side** packet processing. It provides the multi-threaded dispatch pipeline, middleware execution engine, handler compilation, and session state infrastructure.

!!! info "The Engine of the Server"
    While `Nalix.SDK` is designed for client-side consumption, `Nalix.Runtime` is the engine that handles the heavy lifting on the server, managing worker affinity, request routing, and industrial-grade session persistence.

!!! note "Typically consumed via Nalix.Hosting"
    Most projects consume `Nalix.Runtime` indirectly through `Nalix.Hosting`, which wires up the dispatcher and middleware automatically. Use `Nalix.Runtime` directly only when you need full control over the dispatch pipeline.

## Where It Fits

```mermaid
flowchart LR
    A["Nalix.Network (Transport)"] --> B["Nalix.Runtime (Dispatch)"]
    B --> C["Nalix.Codec (Serialization)"]
    B --> D["Nalix.Framework (Registry)"]
    B --> E["Nalix.Abstractions (Contracts)"]
    B --> F["Handler Logic"]
```


## Core Components

### Packet Dispatch

`PacketDispatchChannel` is the engine that processes all incoming network traffic. It manages:

- **Shard-aware worker loops** — Multiple workers (scaled to CPU core count) pull from the dispatch queue in parallel, preventing head-of-line blocking.
- **Priority queueing** — Packets are prioritized by `PacketPriority` (`URGENT`, `HIGH`, `MEDIUM`, `LOW`, `NONE`).
- **Deserialization** — Uses the `PacketRegistry` to convert raw bytes into typed packet instances.
- **Packet middleware execution** — Runs the configured middleware chain before handler invocation.
- **Handler invocation** — Calls the matched handler method with the appropriate context.
- **Return handling** — Translates handler return values into outbound network responses.

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithLogging(logger)
           .WithErrorHandling((ex, opcode) =>
           {
               logger.Error($"dispatch-error opcode=0x{opcode:X4}", ex);
           })
           .WithMiddleware(new MyAuditMiddleware<IPacket>())
           .WithHandler(() => new AccountHandlers())
           .WithHandler(() => new MatchHandlers());
});

dispatch.Activate();
```

### Middleware Pipeline

The runtime supports specialized middleware that executes before high-level handler invocation. `Nalix.Runtime` includes several built-in protection and utility middleware:

| Middleware | Order | Stage | Behavior |
|---|---:|---|---|
| `PermissionMiddleware` | `-50` | `Inbound` | Fail-closed: the packet proceeds only when `[PacketPermission]` exists and its required level is met. |
| `ConcurrencyMiddleware` | `50` | `Inbound` | Enforces `[PacketConcurrencyLimit]` per opcode with optional queuing. |
| `RateLimitMiddleware` | `50` | `Inbound` | Enforces `[PacketRateLimit]` or falls back to global token-bucket throttling. |
| `TimeoutMiddleware` | `75` | `Inbound` | Enforces `[PacketTimeout]` on handler execution. |

!!! warning "Permission default is deny"
    `PermissionMiddleware` intentionally rejects handlers without permission metadata. Do not add it globally unless packet handlers are annotated with the required permission attributes.

### Protection Primitives

The runtime includes advanced throttling and protection primitives used by the middleware:

- **TokenBucketLimiter**: Tracks per-endpoint token state for traffic shaping.
- **PolicyRateLimiter**: Evaluates handler-specific policy from `[PacketRateLimit]` metadata.
- **ConcurrencyGate**: Manages per-opcode execution slots and circuit breaking.
- **DirectiveGuard**: Protects against response directive spamming for failed requests.

### Handler Compilation

Handler methods are discovered and compiled during `Build()`:

- Methods annotated with `[PacketOpcode]` are matched to packet types
- Handler delegates are pre-compiled using expression trees or IL emit to avoid reflection during the hot path
- Handler metadata (permissions, timeouts, rate limits) is resolved once and cached in `PacketMetadata`

### Session Resume

The built-in session resume flow is handled by `SessionHandlers` and backed by `ISessionStore`. It uses the unified `SessionResume` packet with `SessionResumeStage` to manage request/response stages:

1. Client sends a `SessionResume` with `Stage = REQUEST` and a session token
2. Server validates the token against `ISessionStore`
3. Server restores connection state and sends `SessionResume` with `Stage = RESPONSE`

### Routing

Attribute-based routing maps opcodes to handler methods:

```csharp
[PacketController("AccountHandlers")]
public sealed class AccountHandlers
{
    [PacketOpcode(0x2001)]
    [PacketPermission(PermissionLevel.USER)]
    [PacketTimeout(5000)]
    public async ValueTask<AccountResponse> Login(
        IPacketContext<LoginRequest> context)
    {
        // Handler logic
    }
}
```

### Time Synchronization

`TimeSynchronizer` is an optional service that emits `TimeSynchronized` events at a default period of 16 ms, useful for periodic game logic or world updates.

## Handler Return Types

The dispatch pipeline supports multiple return shapes. The internal return handler converts each into the appropriate outbound behavior:

| Return type | Behavior |
| :--- | :--- |
| `TPacket` | Serializes and sends the packet to the caller |
| `Task<TPacket>` / `ValueTask<TPacket>` | Awaits, then serializes and sends |
| `string` | Sends as a text response |
| `byte[]` / `Memory<byte>` | Sends as raw bytes |
| `void` / `Task` / `ValueTask` | No response; side-effect only |

## Diagnostics

Call `dispatch.GenerateReport()` to inspect runtime state:

- Number of active workers
- Queue depth
- Registered handler count
- Middleware chain and limiter statistics

## Related Packages

- [Nalix.Network](./nalix-network.md) — Transport and listeners
- [Nalix.Hosting](./nalix-hosting.md) — Fluent bootstrap
- [Nalix.Framework](./nalix-framework.md) — Packet registry and serialization
- [Nalix.Abstractions](./nalix-abstractions.md) — Shared contracts and primitives

## Key API Pages

- [Packet Dispatch](../api/runtime/routing/packet-dispatch.md)
- [Packet Dispatch Options](../api/options/runtime/packet-dispatch-options.md)
- [Middleware Pipeline](../api/runtime/middleware/pipeline.md)
- [Concurrency Gate](../api/runtime/middleware/concurrency-gate.md)
- [Policy Rate Limiter](../api/runtime/middleware/policy-rate-limiter.md)
- [Token Bucket Limiter](../api/runtime/middleware/token-bucket-limiter.md)
- [Permission Middleware](../api/runtime/middleware/permission-middleware.md)
- [Timeout Middleware](../api/runtime/middleware/timeout-middleware.md)
- [Packet Attributes](../api/abstractions/packet-attributes.md)
- [Handler Return Types](../api/runtime/routing/handler-results.md)
- [Dispatch Options](../api/options/runtime/dispatch-options.md)
- [Session Resume](../api/security/session-resume.md)
