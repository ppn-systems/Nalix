# Nalix.Runtime

`Nalix.Runtime` is the high-performance orchestration layer of the Nalix framework, specifically designed to power **Server-Side** packet processing. It provides the multi-threaded dispatch pipeline, middleware execution engine, handler compilation, and session state infrastructure.

## Source Mapping

- `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs`
- `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs`
- `src/Nalix.Runtime/Dispatching/PacketDispatcherBase.cs`
- `src/Nalix.Runtime/Handlers/SessionHandlers.cs`
- `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs`
- `src/Nalix.Runtime/Sessions/SessionService.cs`
- `src/Nalix.Runtime/Sessions/SessionFactory.cs`
- `src/Nalix.Runtime/Sessions/InMemorySessionStore.cs`
- `src/Nalix.Runtime/Sessions/SessionPersistenceObserver.cs`
- `src/Nalix.Runtime/Options/SessionStoreOptions.cs`

!!! info "The Engine of the Server"
    While `Nalix.SDK` is designed for client-side consumption, `Nalix.Runtime` is the engine that handles the heavy lifting on the server, managing dispatch workers, request routing, and session-resume infrastructure.

!!! note "Typically consumed via Nalix.Hosting"
    Most projects consume `Nalix.Runtime` indirectly through `Nalix.Hosting`, which wires up the dispatcher and middleware automatically. Use `Nalix.Runtime` directly only when you need full control over the dispatch pipeline.

## Where It Fits

```mermaid
flowchart TD
    subgraph Svc ["Service Layer"]
        Runtime["Nalix.Runtime (Dispatch)"]
        Network["Nalix.Network (Transport)"]
    end

    subgraph Core ["Core Layer"]
        Codec["Nalix.Codec (Registry & Serialization)"]
        Framework["Nalix.Framework (DI & Utils)"]
    end

    subgraph Base ["Base Layer"]
        Env["Nalix.Environment (Memory & IO)"]
        Abstractions["Nalix.Abstractions (Contracts)"]
    end

    Runtime --> Codec
    Runtime --> Framework
    Network --> Framework
    
    Codec --> Env
    Framework --> Env
    Env --> Abstractions
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
    options.WithErrorHandling((ex, opcode) =>
           {
               // Custom error hook for handler exceptions
           })
           .WithMiddleware(new MyAuditMiddleware<IPacket>())
           .WithHandler(() => new AccountHandlers())
           .WithHandler(() => new MatchHandlers());
});

dispatch.Activate();
```

!!! info "Diagnostics via DiagnosticListener"
    Runtime emits diagnostics through `Nalix.Runtime.DiagnosticsEvents.Source`
    (a `DiagnosticListener` named `"Runtime"`). Subscribe in your host to bridge
    events into `ILogger`, OpenTelemetry, or any other observability sink.
    Per-instance logger injection (`WithLogging`) has been removed.

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

The built-in session resume flow is handled by `SessionHandlers` and backed by `ISessionService`. It uses the unified `SessionResume` packet with `SessionResumeStage` to manage request/response stages:

1. Client sends a `SessionResume` with `Stage = REQUEST` and a session token
2. Server validates the token against `ISessionService`
3. Server restores connection state and sends `SessionResume` with `Stage = RESPONSE`

In the current source, `src/Nalix.Runtime/Handlers/SessionHandlers.cs` validates proof-of-possession with `HmacKeccak256`, restores the snapshot onto the live connection, stores the connection back into the session service, and returns a fresh session token for the next reconnect.

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

`TimeSynchronizer` is an optional service that emits `TimeSynchronized` events at a default period of 16 ms (~60 Hz), designed for clock synchronization and periodic tick consumers.

### Session Store & Service

`Nalix.Runtime` provides the session-store and session-service implementations. The `ISessionService`, `ISessionFactory`, and `ISessionStore` interfaces provide:

- **Decoupled session persistence** via `SessionPersistenceObserver` subscribing to `IConnectionHub` events.
- **High-performance connection state snapshots** and restoration.
- **TTL-based session retention** with active scavenging via `IWorker` in `InMemorySessionStore`.
- **Atomic consumption** (`ConsumeAsync`) to prevent resumption replay attacks.

## Handler Return Types

The dispatch pipeline supports multiple return shapes. The internal return handler converts each into the appropriate outbound behavior:

| Return type | Behavior |
| :--- | :--- |
| `TPacket` | Serializes and sends the packet to the caller |
| `Task<TPacket>` / `ValueTask<TPacket>` | Awaits, then serializes and sends |
| `byte[]` / `Memory<byte>` / `ReadOnlyMemory<byte>` | Sends as raw bytes |
| `void` / `Task` / `ValueTask` | No response; side-effect only |

## Diagnostics

Runtime diagnostics are published through `Nalix.Runtime.DiagnosticsEvents.Source`
(a `DiagnosticListener` named `"Runtime"`). The `Nalix.Hosting` `DiagnosticChannel`
automatically bridges these events into `ILogger`.

Subscribe to specific event levels:

| Event | Level |
|---|---|
| `Internal.Trace` | Trace |
| `Internal.Debug` | Debug |
| `Internal.Information` | Information |
| `Internal.Warning` | Warning |
| `Internal.Error` | Error |
| `Internal.Critical` | Critical |

Call `dispatch.GenerateReport()` to inspect runtime state:

- Number of active workers
- Queue depth and ready-connection state
- Wake-signal counters
- Top pending connections and per-priority readiness

## Related Packages

- [Nalix.Network](./nalix-network.md) — Transport and listeners
- [Nalix.Hosting](./nalix-hosting.md) — Fluent bootstrap
- [Nalix.Codec](./nalix-codec.md) — Packet registry and serialization
- [Nalix.Framework](./nalix-framework.md) — Shared services and utilities
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
- [Session Store & Service](../api/network/session-store.md)
- [Session Store Options](../api/options/network/session-store-options.md)
- [Session Resume](../api/security/session-resume.md)
