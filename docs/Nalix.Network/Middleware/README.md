# MiddlewarePipeline — Pluggable, Stage-Safe Packet Middleware Framework (for .NET Servers)

The `MiddlewarePipeline<TPacket>` library provides a flexible, stage-driven pipeline for processing inbound and outbound packets in a .NET network/server backend.  
It enables modular, composable protocol stacks, with safe ordering, concurrency, error handling, and performance optimizations (no runtime locks on execution).  
This is the ideal backbone for advanced gateway, firewall, validation, transformation, throttling, or security stacks.

---

## Key Features

- **Middleware Staging:**  
  Supports three middleware execution stages:
  - **Inbound:** Pre-handler processing (validation, rate limit, security, unwrap, etc.)
  - **Outbound:** Post-handler transforms (wrap, audit, etc. — executed in reverse order)
  - **OutboundAlways:** Always runs after handler, even if cancelled/error occurs

- **Flexible Ordering:**  
  Attribute-driven ordering via `[MiddlewareOrder]` and `[MiddlewareStage]` for full control

- **Composable & Thread-Safe:**  
  Register, clear, and reorder middleware at runtime; snapshot-based execution for zero locking during hot-paths

- **Configurable Error Handling:**  
  Catch middleware exceptions, choose to continue or abort the chain, pluggable error logger/handler

- **Reusable for All Packet Types:**  
  Works with any packet/business object type (`IPacket`, or custom DTO), and any server scenario

- **Easy Integration:**  
  Use as a standalone lib or drop into protocol processing path

---

## Usage Example

```csharp
// Register your middleware components (inbound, outbound, transform, etc)
var pipeline = new MiddlewarePipeline<IPacket>();
pipeline.Use(new UnwrapPacketMiddleware());
pipeline.Use(new PermissionMiddleware());
pipeline.Use(new ConcurrencyMiddleware());
pipeline.Use(new RateLimitMiddleware());
pipeline.Use(new TimeoutMiddleware());
pipeline.Use(new WrapPacketMiddleware()); // Outbound

// Configure global error handling behavior
pipeline.ConfigureErrorHandling(
    continueOnError: true,
    errorHandler: (ex, type) => logger.Error($"MW error in {type.Name}: {ex}")
);

// When handling a request:
await pipeline.ExecuteAsync(
    context,              // PacketContext<TPacket> with all connection/packet info
    async ct => { await MyHandler(context.Packet, ct); },  // Final business handler
    cancellationToken     // Cancellation
);
```

---

## Middleware Implementation Contract

Implement a middleware as below (example: Permission check):

```csharp
[MiddlewareOrder(-50)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public class PermissionMiddleware : IPacketMiddleware<IPacket>
{
    public async Task InvokeAsync(
        PacketContext<IPacket> context,
        Func<CancellationToken, Task> next)
    {
        if (context.Attributes.Permission is null
            || context.Attributes.Permission.Level <= context.Connection.Level)
        {
            await next(context.CancellationToken);
            return;
        }

        // Optionally: send a fail message, log, etc...
    }
}
```

---

## Attribute-Driven Ordering & Staging

- **[MiddlewareOrder(N)]**: Set execution order (lower runs first for inbound, last for outbound)
- **[MiddlewareStage(Inbound|Outbound|Both, AlwaysExecute = ...)]**: Bind to pipeline stages

Middleware order is automatically resolved and cached for runtime performance.

---

## Supported Middleware Examples

| Middleware                | Stage        | Use-case                                     |
|---------------------------|--------------|----------------------------------------------|
| `UnwrapPacketMiddleware`  | Inbound      | Decrypt/decompress incoming data             |
| `PermissionMiddleware`    | Inbound      | Permission/auth guard                        |
| `ConcurrencyMiddleware`   | Inbound      | Dynamic request throttling                   |
| `RateLimitMiddleware`     | Inbound      | Global/IP/attribute-driven rate limits       |
| `TimeoutMiddleware`       | Inbound      | Per-packet/handler timeout+fail response     |
| `WrapPacketMiddleware`    | Outbound     | Encrypt/compress outgoing data               |

---

## Error Handling

Configure whether to abort or continue on exception in any middleware. Plug in a custom logger:

```csharp
pipeline.ConfigureErrorHandling(
    continueOnError: false, // Or true to log and skip faulty MW
    errorHandler: (ex, type) => Log.Error($"[Middleware {type.Name}] {ex}")
);
```

---

## Extending / Advanced Usage

- Write custom middleware for: A/B testing, metrics, request manipulation, legacy/modern protocol handling, device policies, circuit breaking, etc.
- Use `Clear()` to remove all middleware and re-register chains dynamically.
- All state is lock-free during execution for high-throughput server deployments.

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025-2026 PPN Corporation.

---

## See Also

- Example protocol implementation: [Protocol.md](../Protocols/README.md)
- Middleware stages/enums: [MiddlewareStageAttribute](./Attributes)
- Custom packet types, server pools — reference server guide
