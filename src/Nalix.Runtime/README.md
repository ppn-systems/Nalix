# Nalix.Runtime

The application-level execution engine. Handles packet routing, sharding, and middleware execution.

## Features

- **PacketDispatchChannel**: Shard-aware execution loops that move packet handling off the network threads.
- **Middleware Pipeline**: Inbound and outbound middleware support ([MiddlewareOrder] aware).
- **Controllers**: Attribute-based routing (`[PacketController]`, `[PacketOpcode]`).
- **Context Injection**: Provides `IPacketContext<T>` to handlers with access to buffers, metadata, and connection state.

## Installation

```bash
dotnet add package Nalix.Runtime
```

## Quick Example: Middleware

```csharp
public class MyLoggingMiddleware<T> : IPacketMiddleware<T> where T : IPacket
{
    public async Task InvokeAsync(PacketContext<T> ctx, Func<CancellationToken, Task> next)
    {
        Console.WriteLine($"In-flight: {typeof(T).Name}");
        await next(ctx.CancellationToken);
    }
}
```

## Documentation

Learn about the [Middleware Pipeline](https://ppn-systems.me/concepts/middleware) and [Shard-Aware Dispatch](https://ppn-systems.me/concepts/architecture).
