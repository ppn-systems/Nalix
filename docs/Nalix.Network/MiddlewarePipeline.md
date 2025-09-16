# Middleware Pipeline Documentation

## Overview

The `MiddlewarePipeline<TPacket>` class provides a flexible, extensible way to chain and execute multiple middleware components for packet processing in .NET networking applications. It enables you to build robust pipelines for inbound (pre-handler) and outbound (post-handler) packet middleware, applying cross-cutting concerns such as rate limiting, authorization, logging, compression, encryption, and more.

---

## Functional Summary

- **Chaining:** Supports ordered chaining of inbound, outbound, and always-outbound middleware.
- **Separation:** Distinguishes between middleware to run before the main handler (`inbound`), after the handler (`outbound`), and always after (`outboundAlways`).
- **Execution:** Builds and executes the middleware chain dynamically, allowing each component to asynchronously process or short-circuit packet handling.
- **Extensibility:** Easily add custom middleware by implementing `IPacketMiddleware<TPacket>`.
- **SOLID/DDD:** Follows Single Responsibility and Open/Closed principles, promoting clean, maintainable, and testable code.

---

## Code Explanation

### Fields

- `_inbound`: List of middleware executed **before** the main handler.
- `_outbound`: List of middleware executed **after** the main handler, **if** `context.SkipOutbound` is `false`.
- `_outboundAlways`: List of middleware always executed after the handler, regardless of `SkipOutbound`.

### Properties

- `IsEmpty`: Returns `true` if no middleware is registered in any stage.

### Core Methods

- **UseInbound(IPacketMiddleware<TPacket>):**  
  Adds a middleware to the inbound (pre-handler) stage.

- **UseOutbound(IPacketMiddleware<TPacket>):**  
  Adds a middleware to the outbound (post-handler) stage.

- **UseOutboundAlways(IPacketMiddleware<TPacket>):**  
  Adds a middleware to always run after the handler (useful for logging, cleanup, etc.).

- **ExecuteAsync(PacketContext<TPacket>, handler, CancellationToken):**  
  Executes the full middleware chain in order: inbound → main handler → outboundAlways → outbound (if not skipped).
  - Builds the call chain so each middleware can call the next.
  - Middleware can perform any logic and decide whether to continue the chain.

- **ExecuteMiddlewareChain(...):**  
  Internal helper to build and execute the delegate chain.

---

## Usage

```csharp
// Example: Registering middleware
var pipeline = new MiddlewarePipeline<IPacket>();
pipeline.UseInbound(new RateLimitMiddleware());
pipeline.UseInbound(new PermissionMiddleware());
pipeline.UseOutbound(new WrapPacketMiddleware());
pipeline.UseOutboundAlways(new LoggingMiddleware());

// Example: Executing the pipeline
await pipeline.ExecuteAsync(
    context,
    async ct => { await HandlePacketAsync(context, ct); },
    cancellationToken
);
```

---

## Example

```csharp
public class LoggingMiddleware : IPacketMiddleware<IPacket>
{
    public async Task InvokeAsync(
        PacketContext<IPacket> context,
        Func<CancellationToken, Task> next,
        CancellationToken ct)
    {
        Console.WriteLine($"Processing packet: {context.Packet.OpCode}");
        await next(ct);
        Console.WriteLine("Finished processing.");
    }
}

// Register and use
var pipeline = new MiddlewarePipeline<IPacket>();
pipeline.UseInbound(new LoggingMiddleware());

await pipeline.ExecuteAsync(
    context,
    async ct => { /* main handler logic */ },
    cancellationToken
);
```

---

## Notes & Security

- **Order Matters:** Middleware is executed in the order it is added.
- **Short-circuiting:** Any middleware can decide to skip the rest of the pipeline (e.g., on validation failure).
- **Async Support:** All middleware and handlers must be asynchronous for scalability.
- **Testability:** Middleware can be tested independently.
- **Extensibility:** Add custom logic for authentication, metrics, etc., by implementing `IPacketMiddleware<TPacket>`.
- **Security:** Always validate and sanitize packet data at appropriate middleware points.

---

## SOLID & DDD Principles

- **Single Responsibility:** Each middleware handles a distinct concern (e.g., rate limiting, logging).
- **Open/Closed:** Add or replace middleware without changing the pipeline implementation.
- **Liskov Substitution:** Any `IPacketMiddleware<TPacket>` can be used interchangeably.
- **Interface Segregation:** Middleware interface is minimal and focused.
- **Dependency Inversion:** Middleware depends on abstractions, not concrete implementations.

**Domain-Driven Design:**  
Middleware represents infrastructure logic. Business rules should reside in handlers or domain services, not middleware.

---

## Additional Remarks

- **Integration:** Designed to work seamlessly with the rest of the packet dispatching framework.
- **Best Practices:** Keep middleware focused and composable. Avoid mixing too many responsibilities in a single middleware component.

---
