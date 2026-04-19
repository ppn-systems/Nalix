# Middleware Pipeline

This page explains how the `MiddlewarePipeline` fits into the Nalix request path and how to leverage it for application-level policy and observability.

## The Middleware Model

Nalix utilizes a single, high-performance middleware layer powered by the `MiddlewarePipeline`. This pipeline executes **after** packet deserialization and metadata resolution, giving you full context for every request.

```mermaid
flowchart LR
    A["Socket frame"] --> B["FramePipeline (Low-level)"]
    B --> C["Deserialize packet"]
    C --> D["Resolve handler metadata"]
    D --> E["MiddlewarePipeline"]
    E --> F["Handler"]
    F --> G["Return handler / reply"]
```

### Middleware Pipeline

The `MiddlewarePipeline` runs with a full `PacketContext<TPacket>`. This context provides the deserialized packet, connection state, and cached handler metadata.

Use it when:

- You need permission checks
- You need timeout enforcement
- You need rate limiting or concurrency limiting
- You need audit logging with handler context
- You need tenant or product policy checks

**Tradeoff:** Because it runs after deserialization, the packet has already been allocated. For early-stage rejection of malformed traffic, rely on `ConnectionGuard` or `Protocol` validation.

## Low-Level Transformations

Low-level buffer operations that were previously handled by "Buffer Middleware" (such as decryption, decompression, and raw frame validation) are now integrated into the **`FramePipeline`**. This pipeline is executed directly by the **Listeners** (TCP/UDP) at the transport layer to ensure maximum performance and zero-allocation processing of raw bytes.

## Execution Order

Middleware executes in **registration order** for the inbound path and **reverse registration order** for the outbound path.

```mermaid
flowchart LR
    M1["Middleware 1 (Inbound)"] --> M2["Middleware 2 (Inbound)"]
    M2 --> H["Handler"]
    H --> M2_Out["Middleware 2 (Outbound)"]
    M2_Out --> M1_Out["Middleware 1 (Outbound)"]
```

Registration example:

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    // Register middleware into the MiddlewarePipeline
    options.WithMiddleware(new PermissionMiddleware());
    options.WithMiddleware(new RateLimitMiddleware());
    options.WithMiddleware(new AuditMiddleware());
});
```

## How Metadata Fits In

Middleware becomes powerful because the dispatcher resolves metadata **before** the pipeline runs.

Middleware can read:

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
| :--- | :--- |
| Reject a malformed frame before packet creation | `Protocol` / `ConnectionGuard` |
| Decrypt or decompress a frame | `FramePipeline` (Transport) |
| Block a packet by permission level | `MiddlewarePipeline` |
| Apply per-handler timeout rules | `MiddlewarePipeline` |
| Read a custom tenant tag from metadata | `MiddlewarePipeline` |
| Rate limit by opcode or endpoint | `MiddlewarePipeline` |
| Audit handler invocations | `MiddlewarePipeline` |

## Common Pitfalls

!!! warning "Pitfall: Forgetting to call next()"
    If a middleware does not call `await next(ct)`, the request pipeline is short-circuited. This is intentional for rejection scenarios but can cause silent drops if done accidentally.

!!! warning "Pitfall: Ordering assumptions"
    Middleware executes in registration order. If your permission middleware runs after your audit middleware, you will audit rejected requests — which may or may not be desirable.

## See it in action

- :fontawesome-solid-screwdriver-wrench: [Custom Middleware End-to-End](../../guides/extensibility/custom-middleware.md) — Create a custom middleware and metadata provider.
- :fontawesome-solid-user-shield: [UDP Auth Flow](../../guides/networking/udp-security.md) — See how authentication middleware is used in a real scenario.
- :fontawesome-solid-arrow-right-arrow-left: [TCP Request/Response](../../guides/networking/tcp-patterns.md) — Observe middleware in a standard TCP flow.
