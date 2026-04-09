# 🛤️ Nalix AI Skill — Custom Middleware Development

This skill provides a deep dive into creating custom middleware for the Nalix processing pipeline.

---

## 🏗️ Middleware Type

### `IPacketMiddleware<T>` (Packet Level)
Operates on the deserialized packet and its `IPacketContext<T>`.

- **Use Case:** Authentication, logging, rate limiting per user, business logic validation.
- **Interface:** `InvokeAsync(IPacketContext<T> context, PacketMiddlewareDelegate next)`.

---

## 📜 Ordering and Stages

### `[MiddlewareOrder(N)]`
Determines the execution order in the pipeline.
- Lower numbers execute first for Inbound.
- Higher numbers execute first for Outbound (if applicable).

### `[MiddlewareStage]`
Specifies which stage of the pipeline the middleware belongs to:
- `Inbound`: Incoming data.
- `Outbound`: Outgoing data.
- `AlwaysExecute`: For outbound, ensures the middleware runs even if the handler fails.

---

## ⚡ Zero-Allocation Implementation

Middleware is executed for **every single packet**. It must be extremely fast.

- **Pooled Instances:** Register middleware as Singletons in the DI container.
- **Avoid Captures:** Do not use `Task.Run` or create closures inside `InvokeAsync`.
- **In-Place Modification:** Modify the `IBufferLease` or `IPacketContext` attributes directly.

---

## 🧪 Implementation Example

### Audit Middleware
```csharp
[MiddlewareOrder(100)]
public sealed class AuditMiddleware : IPacketMiddleware<IPacket>
{
    public async ValueTask InvokeAsync(IPacketContext<IPacket> context, PacketMiddlewareDelegate next)
    {
        // Logic
        await next(context);
    }
}
```

---

## 🛡️ Common Pitfalls

- **Leaking Leases:** If you don't call `next(lease)`, you **MUST** dispose of the lease yourself.
- **Exceptions:** Unhandled exceptions in middleware will typically drop the connection. Wrap critical logic in try/catch.
- **State Confusion:** Since middleware is often a singleton, do not store connection-specific state in fields. Use `context.Attributes` instead.
