# Middleware Usage Guide

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 10–15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Quickstart](../../quickstart.md)

Use this guide when you need to implement request policy, security enforcement, or observability in your Nalix application.

## Core Concepts

In Nalix, middleware is organized into the **`MiddlewarePipeline`**. This pipeline operates on deserialized packets (`IPacketContext<TPacket>`), allowing you to make decisions based on both the packet data and the resolved handler metadata.

```mermaid
flowchart LR
    A["Inbound frame"] --> B["Deserialization"]
    B --> C["MiddlewarePipeline"]
    C --> D["Handler"]
```

## When to use Middleware

Use middleware when you need to enforce logic that applies to many different packet types or handlers.

Typical cases:

- **Permission checks**: Block requests based on connection status or auth level.
- **Timeout rules**: Cancel handler execution if it takes too long.
- **Rate limiting**: Throttling requests to prevent spam.
- **Concurrency limits**: Preventing too many simultaneous executions of heavy handlers.
- **Auditing and tracing**: Logging request details for debugging or compliance.

## A safe build order

For most projects, middleware usually grows cleanly in this order:

1. Start with one simple middleware (e.g., logging).
2. Add metadata-driven policy (using Attributes) only when repeated rules appear across different handlers.
3. Keep middleware focused—prefer multiple specific middlewares over one massive "god" middleware.

## Example: Registering Middleware

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithLogging(logger)
           .WithMiddleware(new PermissionMiddleware())
           .WithMiddleware(new SampleAuditMiddleware<IPacket>())
           .WithHandler(() => new SamplePingHandlers());
});
```

## Low-Level Processing (Non-Middleware)

If you need to perform actions on raw bytes (like custom decryption or transformation) before they become packets, this logic is handled by the **`Protocol`** and the **`FramePipeline`** in the transport layer, rather than the `MiddlewarePipeline`.

## Common mistakes

- Putting too many unrelated policies in one middleware.
- Adding custom metadata before proving you need it.
- Forgetting that middleware order changes behavior (Inbound: registration order; Outbound: reverse order).

## Good default patterns

For public traffic, a good starting setup is:

- `ConnectionGuard` at the transport layer for early admission control.
- `PermissionMiddleware` to ensure only authorized clients reach handlers.
- `RateLimitMiddleware` to protect your resources.

## Read this next

- [Custom Middleware Guide](../extensibility/custom-middleware.md)
- [Custom Metadata Providers](../extensibility/metadata-providers.md)
- [Middleware Concept](../../concepts/runtime/middleware-pipeline.md)
- [Middleware Pipeline API](../../api/runtime/middleware/pipeline.md)
