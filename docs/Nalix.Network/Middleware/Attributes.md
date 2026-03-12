# Middleware Attribute Library — Execution Ordering, Staging, and Pipeline Integration

This package provides core attributes to control middleware ordering, execution stages, and pipeline-managed transformations in any .NET backend/server using a middleware pipeline (e.g., `MiddlewarePipeline<TPacket>`).  
These attributes let you concisely control where, when, and how your middleware gets invoked — without manual pipeline wiring.

---

## Available Attributes

### 1. `MiddlewareOrderAttribute`

Marks the execution **priority/order** of a middleware class in the pipeline.

```csharp
[MiddlewareOrder(50)]
public class MyBusinessMiddleware : IPacketMiddleware<IPacket> { ... }
```

- **Order value meaning:**
  - **Lower = earlier in inbound, later in outbound.**
  - Negative: executes before default (e.g., security, unwrapping)
  - Zero: default order
  - Positive: executes after default (e.g., limits, post-processing)

| Order Value | Common Use Case                                   |
|-------------|---------------------------------------------------|
| -100        | Critical pre-processing (unwrapping, decryption)  |
| -50         | Security/authentication checks                    |
| 0           | Default logic                                     |
| 50          | Business, throttling, rate/concurrency limits     |
| 100         | Post-processing (wrapping, encryption)            |

---

### 2. `MiddlewareStageAttribute`

Indicates **which stage** (inbound, outbound, both) this middleware should run in the pipeline.

```csharp
[MiddlewareStage(MiddlewareStage.Inbound)]
public class AuthGuardMiddleware : IPacketMiddleware<IPacket> { ... }

[MiddlewareStage(MiddlewareStage.Outbound, AlwaysExecute = true)]
public class AuditLogMiddleware : IPacketMiddleware<IPacket> { ... }
```

- **Inbound:** Executed *before* the main handler (verification, unwrapping)
- **Outbound:** Executed *after* handler (logging, wrapping)
- **Both:** Included in both stages

#### `AlwaysExecute` (for Outbound)

- By default, outbound middleware can be **skipped** if packet context signals `SkipOutbound`.
- `AlwaysExecute = true`: Middleware always runs, even if outbound phase is being skipped (e.g., for auditing, hard security policy).

---

### 3. `PipelineManagedTransformAttribute`

```csharp
[PipelineManagedTransform]
public class RawPassthroughPacket : IPacket { ... }
```

- **Purpose:**  
  Marks a packet type as “handled by pipeline,” not by its own per-type transformer logic.  
  Catalog builder skips auto-binding transformer methods for these.
- **Use Case:**  
  For message types that *must not* be transformed by the traversal logic (e.g., raw system/control packets, pipeline-managed records).

---

## Enum: `MiddlewareStage`

Enum values for the above attribute:

```csharp
public enum MiddlewareStage : byte
{
    Inbound = 0,
    Outbound = 1,
    Both = 2
}
```

---

## Best Practices

- Always annotate your middleware with both `[MiddlewareOrder]` and `[MiddlewareStage]` for predictable execution.
- Use negative orders for critical security rules and protocol unwrapping.  
- Use positive orders for limits, logs, or post-processing wrappers.
- Use `AlwaysExecute` for mandatory outbound steps (compliance logging, etc).

---

## Example: Complete Custom Middleware

```csharp
[MiddlewareOrder(-50)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public class PermissionMiddleware : IPacketMiddleware<IPacket>
{
    public async Task InvokeAsync(PacketContext<IPacket> context, Func<CancellationToken, Task> next)
    {
        // ...permission logic here
        await next(context.CancellationToken);
    }
}
```

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2026 PPN Corporation.

---
