# 🛤️ Nalix AI Skill — Runtime Pipeline & Dispatching

This skill explains how packets move through the Nalix server, from the raw bytes at the socket to the execution of application logic in controllers.

---

## 🏗️ The Pipeline Flow

### 1. Inbound Stage
- **Transport Stage**: AEAD verification and Anti-Replay (Sliding window check).
- **Middleware Stage (Packet Level)**: Operates on deserialized `PacketContext<T>`.
- **Built-in**: `RateLimitMiddleware`, `PermissionMiddleware`, `ConcurrencyMiddleware`.
- **Order**: Defined by `[MiddlewareOrder(N)]`.

### 4. Dispatch Stage
- **`PacketHandlerCompiler`**: JIT-compiles (at startup) the most efficient jump sequence for calling your controller methods.
- **Routing:** Matches the `Opcode` to a `PacketController` method.

---

## 📜 Writing Middleware

### Example: Audit Middleware
```csharp
[MiddlewareOrder(100)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class AuditMiddleware : IPacketMiddleware<IPacket>
{
    public async ValueTask InvokeAsync(IPacketContext<IPacket> context, PacketMiddlewareDelegate next)
    {
        Console.WriteLine($"Packet {context.Attributes.Tag} received from {context.Connection.Id}");
        await next(context); // Call next in chain
    }
}
```

---

## ⚡ Dispatch Internals

- **Context Pooling:** `PacketContext<T>` is pooled. Never keep a reference to a context outside the handler scope without calling `Retain()` (if supported) or copying data.
- **Static Handlers:** Prefer `static` methods for handlers to avoid instance allocation and state management complexity.
- **Thread Affinity:** The dispatch loop can be sharded across CPU cores to avoid lock contention on the `ConnectionHub`.

---

## 🛡️ Common Pitfalls

- **Breaking the Chain:** Forgetting to call `await next(context)` will stop the packet from reaching the handler.
- **Duplicate Orders:** Two middlewares with the same `MiddlewareOrder` will result in unpredictable execution (NALIX033).
- **Blocking Calls:** Never use `.Result` or `.Wait()` inside a handler or middleware; use `await`.
