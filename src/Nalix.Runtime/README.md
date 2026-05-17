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
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking.Packets;

public class MyLoggingMiddleware<T> : IPacketMiddleware<T> where T : IPacket
{
    public async ValueTask InvokeAsync(
        IPacketContext<T> context,
        Func<CancellationToken, ValueTask> next)
    {
        Console.WriteLine($"In-flight packet: {typeof(T).Name}");
        await next(context.CancellationToken);
    }
}
```

## Documentation

Learn about the [Middleware Pipeline](https://ppn-system.me/concepts/middleware) and [Shard-Aware Dispatch](https://ppn-system.me/concepts/architecture).
