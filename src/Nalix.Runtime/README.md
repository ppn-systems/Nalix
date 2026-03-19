# Nalix.Runtime

> Application-level execution engine — handles packet routing, shard-aware dispatch, and middleware execution.

## Key Features

| Feature | Description |
| :--- | :--- |
| ⚡ **PacketDispatchChannel** | Shard-aware execution loops that move packet handling off the network threads. |
| 🛤️ **Middleware Pipeline** | Inbound and outbound middleware support with `[MiddlewareOrder]`-aware ordering. |
| 🎯 **Controllers** | Attribute-based routing via `[PacketController]` and `[PacketOpcode]`. |
| 💉 **Context Injection** | Provides `IPacketContext<T>` to handlers with access to buffers, metadata, and connection state. |

## Installation

```bash
dotnet add package Nalix.Runtime
```

## Quick Example: Middleware

```csharp
using Nalix.Runtime.Middleware;

public class MyLoggingMiddleware<T> : IPacketMiddleware<T> where T : IPacket
{
    public async Task InvokeAsync(
        PacketContext<T> ctx,
        Func<CancellationToken, Task> next)
    {
        Console.WriteLine($"In-flight: {typeof(T).Name}");
        await next(ctx.CancellationToken);
    }
}
```

## Documentation

Learn about the [Middleware Pipeline](https://ppn-systems.me/concepts/middleware) and [Shard-Aware Dispatch](https://ppn-systems.me/concepts/architecture).
