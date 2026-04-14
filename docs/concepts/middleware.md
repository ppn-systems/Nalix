# Middleware

This page explains how middleware fits into the Nalix request path and how to choose the right middleware layer for your use case.

## The Two-Layer Model

Nalix has two distinct middleware layers. Choosing the correct layer is more important than any individual middleware implementation.

```mermaid
flowchart LR
    A["Socket frame"] --> B["Buffer middleware"]
    B --> C["Deserialize packet"]
    C --> D["Resolve handler metadata"]
    D --> E["Packet middleware"]
    E --> F["Handler"]
    F --> G["Return handler / reply"]
```

### Buffer Middleware

Buffer middleware runs **before** packet deserialization. It receives raw `IBufferLease` data and has no access to `PacketContext`.

Use it when:

- You need to decrypt or decompress a frame
- You want to reject invalid or suspicious frame shapes early
- You need to perform low-level protocol checks
- You want to stop bad traffic before packet allocation

**Tradeoff:** You get early control, but you do not have typed packet access or handler metadata.

### Packet Middleware

Packet middleware runs **after** deserialization and metadata resolution. It receives `PacketContext<TPacket>` with the deserialized packet, connection state, and cached handler metadata.

Use it when:

- You need permission checks
- You need timeout enforcement
- You need rate limiting or concurrency limiting
- You need audit logging with handler context
- You need tenant or product policy checks

**Tradeoff:** You get full context, but the packet has already been allocated and deserialized.

## Execution Order

Middleware executes in **registration order**. Earlier-registered middleware runs first.

```mermaid
flowchart LR
    M1["Middleware 1 (order -20)"] --> M2["Middleware 2 (order -10)"]
    M2 --> M3["Middleware 3 (order 0)"]
    M3 --> H["Handler"]
```

Registration example:

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    // Buffer middleware (raw frames)
    options.NetworkPipeline.Use(new DecryptionMiddleware());

    // Packet middleware (ordered by registration)
    options.PacketPipeline.Use(new PermissionMiddleware<IPacket>());
    options.PacketPipeline.Use(new RateLimitMiddleware<IPacket>());
    options.PacketPipeline.Use(new AuditMiddleware<IPacket>());
});
```

## How Metadata Fits In

Middleware becomes powerful because dispatch resolves metadata **before** packet middleware runs.

Packet middleware can read:

- `PacketOpcode` — the handler's opcode
- Permission rules (`[PacketPermission]`)
- Timeout rules (`[PacketTimeout]`)
- Rate limit rules (`[PacketRateLimit]`)
- Concurrency limits (`[PacketConcurrencyLimit]`)
- Custom attributes added by `IPacketMetadataProvider`

The typical flow is:

1. Declare attributes on handler methods
2. Optionally enrich them with `IPacketMetadataProvider`
3. Read the resolved metadata inside middleware via `context.Attributes`

## Decision Guide

| Need | Best fit |
|---|---|
| Reject a malformed frame before packet creation | Buffer middleware |
| Decrypt a wrapped payload | Buffer middleware |
| Decompress a frame | Buffer middleware |
| Block a packet by permission level | Packet middleware |
| Apply per-handler timeout rules | Packet middleware |
| Read a custom tenant tag from metadata | Packet middleware |
| Rate limit by opcode or endpoint | Packet middleware |
| Audit handler invocations | Packet middleware |

If you are unsure, start with packet middleware. It is easier to test, easier to debug, and usually the right layer for application-level policy.

## Common Pitfalls

!!! warning "Pitfall: Heavy work in buffer middleware"
    Buffer middleware runs on the transport thread path. Expensive operations (database lookups, external API calls) should be deferred to packet middleware or the handler itself.

!!! warning "Pitfall: Forgetting to call next()"
    If a middleware does not call `await next(ct)`, the request pipeline is short-circuited. This is intentional for rejection scenarios but can cause silent drops if done accidentally.

!!! warning "Pitfall: Ordering assumptions"
    Middleware executes in registration order. If your permission middleware runs after your audit middleware, you will audit rejected requests — which may or may not be desirable.

## Recommended Next Pages

- [Choose the Right Building Block](./choose-the-right-building-block.md) — Component selection guide
- [Middleware Pipeline](../api/runtime/middleware/pipeline.md) — Pipeline API reference
- [Packet Metadata](../api/runtime/routing/packet-metadata.md) — Metadata API reference
- [Custom Middleware End-to-End](../guides/custom-middleware-end-to-end.md) — Build a middleware from scratch
